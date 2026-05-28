using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Facebook;
using Thiccdal.Remote.Facebook;

namespace Thiccdal.Remote.Facebook.Tests;

public sealed class FacebookGraphClientTests
{
    [Fact]
    public async Task WhenCreateLiveVideo_ThenGraphApiPostIsMade()
    {
        var handler = new CapturingMessageHandler(FacebookTestData.LiveVideoJson(new FacebookLiveVideo
        {
            Id = "live-1",
            StreamUrl = "rtmp://facebook/live/key",
            SecureStreamUrl = "rtmps://facebook/live/key"
        }));
        FacebookGraphClient client = CreateClient(handler);

        FacebookLiveVideo response = await client.CreateLiveVideo(
            "page-1",
            "token-1",
            "My title",
            "My description",
            "EVERYONE");

        string body = await handler.ReadContent();

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/v21.0/page-1/live_videos", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Contains("\"status\":\"LIVE_NOW\"", body);
        Assert.Contains("\"title\":\"My title\"", body);
        Assert.Contains("\"description\":\"My description\"", body);
        Assert.Contains("\"value\":\"EVERYONE\"", body);
        Assert.Contains("\"access_token\":\"token-1\"", body);
        Assert.Equal("live-1", response.Id);
    }

    [Fact]
    public async Task WhenStopRelayCalled_ThenEndLiveVideoPostIsMade()
    {
        var handler = new CapturingMessageHandler("""{"success":true}""");
        FacebookGraphClient client = CreateClient(handler);

        await client.EndLiveVideo("live-1", "token-1");

        string body = await handler.ReadContent();

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/v21.0/live-1", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Contains("\"end_live_video\":true", body);
        Assert.Contains("\"access_token\":\"token-1\"", body);
    }

    [Fact]
    public async Task WhenGettingComments_ThenSinceTimestampIsSent()
    {
        var handler = new CapturingMessageHandler(FacebookTestData.CommentsJson(
            FacebookTestData.CreateComment(
                id: "comment-1",
                message: "Hello!",
                userId: "psid-1",
                displayName: "Viewer",
                createdTime: "2024-06-01T14:05:00+0000")));
        FacebookGraphClient client = CreateClient(handler);

        IReadOnlyList<FacebookComment> comments = await client.GetComments(
            "live-1",
            "token-1",
            DateTimeOffset.Parse("2024-06-01T14:05:00+00:00"));

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal(
            "/v21.0/live-1/comments?fields=id,message,from,created_time&access_token=token-1&since=1717250700",
            handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Single(comments);
    }

    [Fact]
    public async Task WhenPostingComment_ThenMessageBodyIsSerialized()
    {
        var handler = new CapturingMessageHandler("""{"id":"comment-1"}""");
        FacebookGraphClient client = CreateClient(handler);

        await client.PostComment("live-1", "token-1", "hello world");

        string body = await handler.ReadContent();

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/v21.0/live-1/comments", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Contains("\"message\":\"hello world\"", body);
        Assert.Contains("\"access_token\":\"token-1\"", body);
    }

    [Fact]
    public async Task WhenUpdatingLiveVideo_ThenTitleAndDescriptionAreSerialized()
    {
        var handler = new CapturingMessageHandler("""{"success":true}""");
        FacebookGraphClient client = CreateClient(handler);

        await client.UpdateLiveVideo("live-1", "token-1", "New title", "New description");

        string body = await handler.ReadContent();

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/v21.0/live-1", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Contains("\"title\":\"New title\"", body);
        Assert.Contains("\"description\":\"New description\"", body);
    }

    [Fact]
    public async Task WhenGettingReactions_ThenReactionsEndpointIsUsed()
    {
        var handler = new CapturingMessageHandler(FacebookTestData.ReactionsJson(
            FacebookTestData.CreateReaction(
                id: "reaction-1",
                type: "LIKE",
                name: "Viewer")));
        FacebookGraphClient client = CreateClient(handler);

        IReadOnlyList<FacebookReaction> reactions = await client.GetReactions("live-1", "token-1");

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal(
            "/v21.0/live-1/reactions?fields=type,name,id&access_token=token-1",
            handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.Single(reactions);
        Assert.Equal("LIKE", reactions[0].Type);
    }

    private static FacebookGraphClient CreateClient(HttpMessageHandler httpMessageHandler)
    {
        return new FacebookGraphClient(
            Options.Create(new FacebookOptions
            {
                GraphApiBaseAddress = "https://graph.facebook.com/",
                GraphApiVersion = "v21.0"
            }),
            NullLogger<FacebookGraphClient>.Instance,
            new TestHttpClientFactory(httpMessageHandler));
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
                BaseAddress = new Uri("https://graph.facebook.com/v21.0/")
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

        public Task<string> ReadContent()
        {
            return Task.FromResult(LastRequestContent);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestContent = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
        }
    }
}
