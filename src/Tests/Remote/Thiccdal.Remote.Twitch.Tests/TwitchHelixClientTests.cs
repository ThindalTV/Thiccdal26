using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchHelixClientTests
{
    [Fact]
    public async Task WhenGettingStreamState_ThenUsesHelixStreamsEndpointAndHeaders()
    {
        var messageHandler = new CapturingMessageHandler("""{"data":[]}""");
        var client = CreateClient(messageHandler);

        await client.GetStreamState(new TwitchChatConnectionProfile
        {
            BotUsername = "riverbot",
            BotUserId = "24680",
            TargetChannel = "thindal",
            BroadcasterId = "12345"
        });

        Assert.Equal(HttpMethod.Get, messageHandler.LastRequest?.Method);
        Assert.Equal("/helix/streams?user_id=12345", messageHandler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Equal("Bearer", messageHandler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("token", messageHandler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("client-id", messageHandler.LastRequest?.Headers.GetValues("Client-Id").Single());
    }

    [Fact]
    public async Task WhenHelixReturnsStreamData_ThenStreamStateIsLive()
    {
        var client = CreateClient(new CapturingMessageHandler("""{"data":[{"id":"stream-1","title":"Thiccdal Live","game_name":"Science & Technology","tags":["dotnet","blazor"],"started_at":"2024-06-01T14:00:00Z"}]}"""));

        TwitchStreamState state = await client.GetStreamState(new TwitchChatConnectionProfile
        {
            BotUsername = "riverbot",
            BotUserId = "24680",
            TargetChannel = "thindal",
            BroadcasterId = "12345"
        });

        Assert.True(state.IsLive);
        Assert.Equal("Thiccdal Live", state.Title);
        Assert.Equal("Science & Technology", state.Category);
        Assert.Equal(["dotnet", "blazor"], state.Tags);
        Assert.Equal(new DateTimeOffset(2024, 6, 1, 14, 0, 0, TimeSpan.Zero), state.StartedAt);
    }

    [Fact]
    public async Task WhenSendingChatMessage_ThenPostsBroadcasterAndSenderIds()
    {
        var messageHandler = new CapturingMessageHandler("""{"data":[{"message_id":"message-1","is_sent":true,"drop_reason":null}]}""");
        var client = CreateClient(messageHandler);

        TwitchSendMessageResult result = await client.SendChatMessage(
            new TwitchChatConnectionProfile
            {
                BotUsername = "riverbot",
                BotUserId = "24680",
                TargetChannel = "thindal",
                BroadcasterId = "12345"
            },
            "hello world");

        string requestBody = await messageHandler.ReadContent();

        Assert.Equal(HttpMethod.Post, messageHandler.LastRequest?.Method);
        Assert.Equal("/helix/chat/messages", messageHandler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Contains("\"broadcaster_id\":\"12345\"", requestBody);
        Assert.Contains("\"sender_id\":\"24680\"", requestBody);
        Assert.Contains("\"message\":\"hello world\"", requestBody);
        Assert.True(result.IsSuccessful);
        Assert.Equal("message-1", result.MessageId);
    }

    [Fact]
    public async Task WhenHelixDropsChatMessage_ThenFailureDetailsAreReturned()
    {
        var client = CreateClient(new CapturingMessageHandler(
            """{"data":[{"message_id":"message-1","is_sent":false,"drop_reason":{"code":"automod_held","message":"held for review"}}]}"""));

        TwitchSendMessageResult result = await client.SendChatMessage(
            new TwitchChatConnectionProfile
            {
                BotUsername = "riverbot",
                BotUserId = "24680",
                TargetChannel = "thindal",
                BroadcasterId = "12345"
            },
            "hello world");

        Assert.False(result.IsSuccessful);
        Assert.Equal("automod_held", result.FailureCode);
        Assert.Equal("held for review", result.FailureMessage);
    }

    [Fact]
    public async Task WhenGettingEventSubscriptions_ThenUsesEventSubEndpoint()
    {
        var handler = new CapturingMessageHandler("""{"data":[{"id":"sub-1","type":"channel.follow","version":"2","condition":{"broadcaster_user_id":"12345","moderator_user_id":"24680"}}]}""");
        var client = CreateClient(handler);

        IReadOnlyList<TwitchEventSubSubscription> subscriptions = await client.GetEventSubscriptions();

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/helix/eventsub/subscriptions", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Single(subscriptions);
        Assert.Equal("channel.follow", subscriptions[0].Type);
        Assert.Equal("24680", subscriptions[0].Condition["moderator_user_id"]);
    }

    [Fact]
    public async Task WhenCreatingEventSubscription_ThenPostsWebsocketSessionAndCondition()
    {
        var handler = new CapturingMessageHandler("""{"data":[]}""");
        var client = CreateClient(handler);

        await client.CreateEventSubscription(new TwitchEventSubSubscriptionRequest
        {
            Type = "channel.follow",
            Version = "2",
            SessionId = "session-1",
            Condition = new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = "12345",
                ["moderator_user_id"] = "24680"
            }
        });

        string body = await handler.ReadContent();

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/helix/eventsub/subscriptions", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Contains("\"type\":\"channel.follow\"", body);
        Assert.Contains("\"session_id\":\"session-1\"", body);
        Assert.Contains("\"moderator_user_id\":\"24680\"", body);
    }

    [Fact]
    public async Task WhenUpdatingChannelInfo_ThenPatchesChannelsEndpointWithResolvedCategory()
    {
        SequenceMessageHandler handler = new(
            """{"data":[{"id":"509658","name":"Just Chatting"}]}""",
            string.Empty,
            HttpStatusCode.OK,
            HttpStatusCode.NoContent);
        TwitchHelixClient client = CreateClient(handler);

        await client.UpdateChannelInfo(
            new TwitchChatConnectionProfile
            {
                BotUsername = "riverbot",
                BotUserId = "24680",
                TargetChannel = "thindal",
                BroadcasterId = "12345"
            },
            "Pre-live title",
            "Just Chatting");

        Assert.Equal(HttpMethod.Patch, handler.Requests[1].Method);
        Assert.Equal("/helix/channels?broadcaster_id=12345", handler.Requests[1].RequestUri?.PathAndQuery);
        Assert.Contains("\"title\":\"Pre-live title\"", handler.RequestBodies[1]);
        Assert.Contains("\"game_id\":\"509658\"", handler.RequestBodies[1]);
    }

    [Fact]
    public async Task WhenUpdatingChannelInfoWithUnknownCategory_ThenThrowsPlatformOperationException()
    {
        SequenceMessageHandler handler = new("""{"data":[]}""");
        TwitchHelixClient client = CreateClient(handler);

        PlatformOperationException exception = await Assert.ThrowsAsync<PlatformOperationException>(() => client.UpdateChannelInfo(
            new TwitchChatConnectionProfile
            {
                BotUsername = "riverbot",
                BotUserId = "24680",
                TargetChannel = "thindal",
                BroadcasterId = "12345"
            },
            "Pre-live title",
            "Unknown category"));

        Assert.Equal("Twitch category 'Unknown category' was not found.", exception.Message);
    }

    private static TwitchHelixClient CreateClient(HttpMessageHandler httpMessageHandler)
    {
        return new TwitchHelixClient(
            Options.Create(new TwitchOptions
            {
                ClientId = "client-id",
                Helix = new TwitchHelixOptions
                {
                    BaseAddress = "https://api.twitch.tv/helix/"
                }
            }),
            new TestTokenManager(),
            NullLogger<TwitchHelixClient>.Instance,
            new TestHttpClientFactory(httpMessageHandler));
    }

    private sealed class TestTokenManager : ITwitchTokenManager
    {
        public Task<string?> GetToken(CancellationToken cancellationToken = default) => Task.FromResult<string?>("token");

        public Task<bool> HasToken(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task RefreshToken(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoreToken(string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Revoke(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetAuthorizationUrl() => string.Empty;

        public bool ValidateAndConsumeState(string state) => true;
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
                BaseAddress = new Uri("https://api.twitch.tv/helix/")
            };
        }
    }

    private sealed class CapturingMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastRequestContent { get; private set; } = string.Empty;

        public CapturingMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        public Task<string> ReadContent() => Task.FromResult(LastRequestContent);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestContent = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };

            return response;
        }
    }

    private sealed class SequenceMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(string Content, HttpStatusCode StatusCode)> _responses;

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> RequestBodies { get; } = [];

        public SequenceMessageHandler(params string[] responseContent)
            : this(responseContent[0], responseContent.Length > 1 ? responseContent[1] : string.Empty)
        {
        }

        public SequenceMessageHandler(
            string firstContent,
            string secondContent,
            HttpStatusCode firstStatusCode = HttpStatusCode.OK,
            HttpStatusCode secondStatusCode = HttpStatusCode.OK)
        {
            _responses = new Queue<(string Content, HttpStatusCode StatusCode)>();
            _responses.Enqueue((firstContent, firstStatusCode));
            _responses.Enqueue((secondContent, secondStatusCode));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            (string content, HttpStatusCode statusCode) = _responses.Dequeue();
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }
}
