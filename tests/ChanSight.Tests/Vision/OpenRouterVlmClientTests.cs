using System.Net;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class OpenRouterVlmClientTests
{
    [Fact]
    public void DefaultModel_ContainsVendorPrefix()
    {
        OpenRouterVlmClient.DefaultModel.Should().Be("deepseek/deepseek-v4-flash-vision-exp");
    }

    [Fact]
    public async Task CompleteAsync_Success_SendsVendorModelAndReturnsContent()
    {
        const string contentJson = """
            { "choices": [ { "message": { "content": "ok" } } ] }
            """;
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(contentJson),
        });
        using var http = new HttpClient(handler);
        var client = new OpenRouterVlmClient(http, apiKey: "test-key");

        var result = await client.CompleteAsync("prompt", Array.Empty<(string mime, byte[] data)>(), CancellationToken.None);

        result.Should().Be("ok");
        handler.CallCount.Should().Be(1);
        handler.LastRequestBody.Should().Contain("\"deepseek/deepseek-v4-flash-vision-exp\"");
    }

    [Fact]
    public async Task CompleteAsync_Non2xx_ThrowsVlmUnavailableException_AndSendsOnce()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var http = new HttpClient(handler);
        var client = new OpenRouterVlmClient(http, apiKey: "test-key");

        var act = async () => await client.CompleteAsync(
            "prompt",
            Array.Empty<(string mime, byte[] data)>(),
            CancellationToken.None);

        await act.Should().ThrowAsync<VlmUnavailableException>();
        handler.CallCount.Should().Be(1);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public int CallCount { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return _responder(request);
        }
    }
}