using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Thiccdal.AI;
using Thiccdal.Infrastructure.AI;

namespace Thiccdal.AI.Tests;

public sealed class OpenAiCompatibleChatClientTests
{
    [Fact]
    public async Task WhenCompletingChat_ThenPostsOpenAiCompatiblePayload()
    {
        using TestOpenAiEndpoint endpoint = new();
        OpenAiCompatibleChatClient client = new(
            Options.Create(
                new OpenAiOptions
                {
                    Endpoint = endpoint.Endpoint,
                    ApiKey = string.Empty,
                    RequestTimeoutSeconds = 10
                }));

        AiChatCompletionResult result = await client.CompleteChat(
            new AiChatCompletionRequest(
                "qwen2.5",
                [
                    new AiChatMessage(AiChatMessageRole.System, "Classify this."),
                    new AiChatMessage(AiChatMessageRole.User, "Message: What game is next?")
                ],
                0.1d,
                32));

        string requestBody = await endpoint.ReadRequestBody();

        Assert.Equal("/v1/chat/completions", endpoint.RequestPath);
        Assert.Contains("\"model\":\"qwen2.5\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"temperature\":0.1", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"max_completion_tokens\":32", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"system\"", requestBody, StringComparison.Ordinal);
        Assert.Equal("{\"isQuestion\":true}", result.Content);
    }

    [Fact]
    public async Task WhenAssistantContentIsMissing_ThenThrows()
    {
        using TestOpenAiEndpoint endpoint = new("""{"id":"chatcmpl-1","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":""}}]}""");
        OpenAiCompatibleChatClient client = new(
            Options.Create(
                new OpenAiOptions
                {
                    Endpoint = endpoint.Endpoint,
                    ApiKey = string.Empty,
                    RequestTimeoutSeconds = 10
                }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CompleteChat(
            new AiChatCompletionRequest(
                "qwen2.5",
                [new AiChatMessage(AiChatMessageRole.User, "Hello")],
                0.1d,
                8)));
    }

    private sealed class TestOpenAiEndpoint : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _listenerTask;
        private readonly string _responsePayload;

        public TestOpenAiEndpoint(string? responsePayload = null)
        {
            int port = GetPort();
            Endpoint = $"http://127.0.0.1:{port}/v1";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _responsePayload = responsePayload
                ?? """{"id":"chatcmpl-1","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{\"isQuestion\":true}"}}]}""";
            _listenerTask = Listen();
        }

        public string Endpoint { get; }

        public string RequestPath { get; private set; } = string.Empty;

        public string RequestBody { get; private set; } = string.Empty;

        public async Task<string> ReadRequestBody()
        {
            await _listenerTask;
            return RequestBody;
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Close();
        }

        private async Task Listen()
        {
            HttpListenerContext context = await _listener.GetContextAsync();
            RequestPath = context.Request.Url?.AbsolutePath ?? string.Empty;

            using StreamReader reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            RequestBody = await reader.ReadToEndAsync();

            byte[] responseBytes = Encoding.UTF8.GetBytes(_responsePayload);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes);
            context.Response.Close();
        }

        private static int GetPort()
        {
            using System.Net.Sockets.TcpListener listener =
                new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
