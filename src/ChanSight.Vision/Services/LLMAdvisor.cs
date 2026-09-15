using System.Text.Json;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

/// <summary>
/// External-triggered, text-only global advisor. Produces exactly one suggestion
/// per key event via a single <see cref="IVlmClient"/> text completion, strictly
/// validates the returned JSON, and enforces a fixed per-instance call budget so
/// the advisory side channel can never spend the LLM without bound.
/// </summary>
public sealed class LLMAdvisor
{
    private readonly IVlmClient _client;
    private int _callCount;

    public LLMAdvisor(IVlmClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<AdvisorSuggestion> AdviseAsync(AdvisorEvent e, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (Interlocked.Increment(ref _callCount) > Budget.MaxCalls)
            throw new InvalidOperationException("advisor budget exceeded");

        var prompt = BuildPrompt(e);
        var raw = await _client
            .CompleteAsync(prompt, Array.Empty<(string mime, byte[] data)>(), ct)
            .ConfigureAwait(false);

        return Parse(raw);
    }

    private static AdvisorSuggestion Parse(string raw)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Advisor returned malformed JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Advisor response must be a JSON object.");

            return new AdvisorSuggestion(
                RequireNonEmptyString(root, "suggestion"),
                RequireNonEmptyString(root, "reason"),
                RequireConfidence(root, "confidence"));
        }
    }

    private static string RequireNonEmptyString(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Advisor response is missing a valid '{field}' field.");

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"Advisor '{field}' must not be empty.");

        return text!;
    }

    private static double RequireConfidence(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var value) || !value.TryGetDouble(out var confidence))
            throw new InvalidOperationException($"Advisor response is missing a valid '{field}' field.");

        if (confidence < 0.0 || confidence > 1.0)
            throw new InvalidOperationException($"Advisor '{field}' must be within [0, 1], got {confidence}.");

        return confidence;
    }

    private static string BuildPrompt(AdvisorEvent e)
    {
        return $$"""
            你是《金铲铲之战》全局参谋。下面给出一个关键节点事件,请给出全局策略建议。

            事件类型: {{e.Type}}
            场景上下文: {{e.Context}}

            只输出一个 JSON 对象,不要输出任何解释、前缀或 markdown 代码块,格式必须为:
            {
              "suggestion": "建议内容(非空字符串)",
              "reason": "理由(非空字符串)",
              "confidence": 0 到 1 之间的浮点数
            }
            """;
    }

    /// <summary>
    /// Fixed per-instance call budget. Once exceeded the advisor refuses further
    /// calls to bound LLM spend.
    /// </summary>
    public static class Budget
    {
        public const int MaxCalls = 100;
    }
}