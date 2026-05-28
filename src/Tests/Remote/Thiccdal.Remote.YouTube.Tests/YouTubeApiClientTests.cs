using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.YouTube;
using Thiccdal.Remote.YouTube;

namespace Thiccdal.Remote.YouTube.Tests;

public sealed class YouTubeApiClientTests
{
    [Fact]
    public async Task WhenGettingActiveBroadcast_ThenLiveChatIdComesFromBroadcastSnippet()
    {
        StubHttpMessageHandler handler = new StubHttpMessageHandler(
            """
            {
              "items": [
                {
                  "id": "broadcast-123",
                  "snippet": {
                    "title": "Live stream",
                    "description": "Chat enabled",
                    "liveChatId": "live-chat-456"
                  },
                  "contentDetails": {
                    "boundStreamId": "stream-789"
                  },
                  "status": {
                    "lifeCycleStatus": "live"
                  }
                }
              ]
            }
            """);

        YouTubeApiClient client = CreateClient(handler);

        YouTubeBroadcastInfo? broadcast = await client.GetActiveBroadcast();

        Assert.NotNull(broadcast);
        Assert.Equal("live-chat-456", broadcast.LiveChatId);
    }

    [Fact]
    public async Task WhenSendingLiveChatMessage_ThenRequestTargetsLiveChatInsertEndpoint()
    {
        StubHttpMessageHandler handler = new StubHttpMessageHandler("{}");
        YouTubeApiClient client = CreateClient(handler);

        await client.SendLiveChatMessage("live-chat-456", "Hello chat!");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://www.googleapis.com/youtube/v3/liveChat/messages?part=snippet", handler.LastRequest.RequestUri?.ToString());

        string body = handler.LastRequestBody;
        Assert.Contains("\"liveChatId\":\"live-chat-456\"", body, StringComparison.Ordinal);
        Assert.Contains("\"messageText\":\"Hello chat!\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenPollingIntervalHintIsMissing_ThenFallbackIntervalIsUsed()
    {
        StubHttpMessageHandler handler = new StubHttpMessageHandler(
            """
            {
              "items": [],
              "nextPageToken": "next-page"
            }
            """);
        YouTubeApiClient client = CreateClient(handler);

        YouTubeLiveChatPollResult result = await client.PollLiveChat("live-chat-456", null);

        Assert.Equal(5000, result.PollingIntervalMillis);
    }

    [Fact]
    public async Task WhenUpdatingBroadcastInfoFails_ThenPlatformOperationExceptionIsThrown()
    {
        StubHttpMessageHandler handler = new StubHttpMessageHandler("{}", HttpStatusCode.BadRequest);
        YouTubeApiClient client = CreateClient(handler);

        PlatformOperationException exception = await Assert.ThrowsAsync<PlatformOperationException>(() =>
            client.UpdateBroadcastInfo("broadcast-123", "Title", "Description"));

        Assert.Contains("YouTube broadcast update failed", exception.Message, StringComparison.Ordinal);
    }

    private static YouTubeApiClient CreateClient(StubHttpMessageHandler handler)
    {
        return new YouTubeApiClient(
            Options.Create(new YouTubeOptions()),
            new StubTokenManager(),
            new StubHttpClientFactory(handler),
            NullLogger<YouTubeApiClient>.Instance);
    }

    private sealed class StubTokenManager : IYouTubeTokenManager
    {
        public string GetAuthorizationUrl()
        {
            return string.Empty;
        }

        public bool ValidateAndConsumeState(string state)
        {
            return true;
        }

        public Task StoreToken(string authorizationCode, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetToken(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>("access-token");
        }

        public Task<bool> HasToken(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task RevokeToken(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri(YouTubeOptions.DefaultApiBaseAddress, UriKind.Absolute)
            };
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
