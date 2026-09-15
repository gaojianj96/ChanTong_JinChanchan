using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChanSight.Vision.Interfaces;

namespace ChanSight.Vision.Services;

/// <summary>
/// OpenRouter chat-completions client. Sends a multimodal prompt (text + base64
/// images) with a Bearer key from the OPENROUTER_API_KEY environment variable.
/// Performs exactly one send attempt and surfaces any failure (transport error,
/// timeout or non-2xx response) as <see cref="VlmUnavailableException"/>.
/// </summary>
public sealed class OpenRouterVlmClient : IVlmClient
{
    public const string DefaultEndpoint = "https://openrouter.ai/api/v1/chat/completions";
    public const string DefaultModel = "deepseek/deepseek-v4-flash-vision-exp";
    public const string ApiKeyEnvironmentVariable = "OPENROUTER_API_KEY";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;

    public OpenRouterVlmClient(HttpClient httpClient, string? model = null, string? apiKey = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model!;
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable) ?? string.Empty;
    }

    public async Task<string> CompleteAsync(
        string prompt,
        IReadOnlyList<(string mime, byte[] data)> images,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(images);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RequestTimeout);

        // Retry is intentionally centralized in VlmRecognitionAdapter (a single
        // retry there). This client sends exactly once and never retries on its
        // own; otherwise the two retry layers would stack to 4 requests worst
        // case (FIX-V3-2). Every failure surface is translated into
        // VlmUnavailableException (FIX-V3-3) so the adapter degrades uniformly.
        try
        {
            using var request = BuildRequest(prompt, images);
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return await ReadCompletionContentAsync(response, ct).ConfigureAwait(false);

            throw new VlmUnavailableException(
                $"OpenRouter returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new VlmUnavailableException("OpenRouter VLM request failed.", ex);
        }
        catch (TaskCanceledException)
        {
            throw new VlmUnavailableException(
                $"OpenRouter request timed out after {RequestTimeout.TotalSeconds:0} seconds.");
        }
    }

    private HttpRequestMessage BuildRequest(string prompt, IReadOnlyList<(string mime, byte[] data)> images)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, DefaultEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(BuildRequestBody(prompt, images), Encoding.UTF8, "application/json");
        return request;
    }

    private string BuildRequestBody(string prompt, IReadOnlyList<(string mime, byte[] data)> images)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", _model);

            writer.WriteStartArray("messages");
            writer.WriteStartObject();
            writer.WriteString("role", "user");

            writer.WritePropertyName("content");
            if (images.Count == 0)
            {
                writer.WriteStringValue(prompt);
            }
            else
            {
                writer.WriteStartArray();

                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteString("text", prompt);
                writer.WriteEndObject();

                foreach (var (mime, data) in images)
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", "image_url");
                    writer.WriteStartObject("image_url");
                    writer.WriteString("url", $"data:{mime};base64,{Convert.ToBase64String(data)}");
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task<string> ReadCompletionContentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                throw new VlmUnavailableException("OpenRouter response is missing 'choices'.");
            }

            var first = choices[0];
            if (!first.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content))
            {
                throw new VlmUnavailableException("OpenRouter response is missing 'message.content'.");
            }

            return content.ValueKind == JsonValueKind.String ? content.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException ex)
        {
            throw new VlmUnavailableException("OpenRouter returned malformed JSON.", ex);
        }
    }
}