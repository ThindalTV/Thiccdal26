using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.LmStudio;
using Thiccdal.Remote.LMStudio;

namespace Thiccdal.Remote.LMStudio.Tests;

public sealed class LmStudioClientTests
{
    [Fact]
    public async Task WhenCompletingChat_ThenPostsOpenAiCompatiblePayload()
    {
        CapturingMessageHandler handler = new(
            """{"model":"qwen2.5","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{\"isQuestion\":true}"}}]}""");
        LmStudioClient client = CreateClient(handler);

        LmStudioChatCompletionResult result = await client.CompleteChat(
            new LmStudioChatCompletionRequest(
                "qwen2.5",
                [
                    new LmStudioChatMessage("system", "Classify this."),
                    new LmStudioChatMessage("user", "Message: What game is next?")
                ],
                0.1d,
                32));

        string requestBody = await handler.ReadContent();

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/v1/chat/completions", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Contains("\"model\":\"qwen2.5\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"temperature\":0.1", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"max_tokens\":32", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"system\"", requestBody, StringComparison.Ordinal);
        Assert.Equal("{\"isQuestion\":true}", result.Content);
    }

    [Fact]
    public async Task WhenAssistantContentIsMissing_ThenThrows()
    {
        LmStudioClient client = CreateClient(new CapturingMessageHandler("""{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":null}}]}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CompleteChat(
            new LmStudioChatCompletionRequest(
                "qwen2.5",
                [new LmStudioChatMessage("user", "Hello")],
                0.1d,
                8)));
    }

    private static LmStudioClient CreateClient(HttpMessageHandler messageHandler)
    {
        return new LmStudioClient(
            Options.Create(new LmStudioOptions
            {
                BaseAddress = "http://localhost:1234/"
            }),
            NullLogger<LmStudioClient>.Instance,
            new TestHttpClientFactory(messageHandler));
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _messageHandler;

        public TestHttpClientFactory(HttpMessageHandler messageHandler)
        {
            _messageHandler = messageHandler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_messageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://localhost:1234/", UriKind.Absolute)
            };
        }
    }

    private sealed class CapturingMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public CapturingMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastRequestContent { get; private set; } = string.Empty;

        public Task<string> ReadContent() => Task.FromResult(LastRequestContent);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestContent = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
        }
    }
}
