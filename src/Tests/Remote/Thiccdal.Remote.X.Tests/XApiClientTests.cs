using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.X;
using Thiccdal.Remote.X;

namespace Thiccdal.Remote.X.Tests;

public class XApiClientTests
{
    [Fact]
    public async Task WhenPollReplies_ThenMapsReplyAuthorAndCreatedAt()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(
            """
            {
              "data": [
                {
                  "id": "1234567890",
                  "text": "Hello stream!",
                  "author_id": "987654321",
                  "created_at": "2024-06-01T14:05:00.000Z"
                }
              ],
              "includes": {
                "users": [
                  {
                    "id": "987654321",
                    "name": "River",
                    "username": "river_handle"
                  }
                ]
              },
              "meta": {
                "newest_id": "1234567890"
              }
            }
            """,
            remaining: 2,
            resetUnixSeconds: 1735689660));

        XApiClient client = CreateClient(handler);

        XReplyPollResult result = await client.PollReplies("broadcast-1", null);

        XTweetReply reply = Assert.Single(result.Replies);
        Assert.Equal("987654321", reply.AuthorId);
        Assert.Equal("River", reply.Author.Name);
        Assert.Equal(DateTimeOffset.Parse("2024-06-01T14:05:00Z"), reply.CreatedAt);
        Assert.Equal(2, result.RateLimit.Remaining);
    }

    [Fact]
    public async Task WhenSendReply_ThenPostsReplyBodyAndOAuthHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return CreateJsonResponse("""{"data":{"id":"tweet-2"}}""");
        });

        XApiClient client = CreateClient(
            handler,
            new XOptions
            {
                ApiBaseAddress = "https://api.twitter.com/",
                ApiVersion = "2",
                ApiKey = "consumer-key",
                ApiKeySecret = "consumer-secret",
                AccessToken = "access-token",
                AccessTokenSecret = "access-secret"
            });

        await client.SendReply("broadcast-1", "Hello X");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://api.twitter.com/2/tweets", capturedRequest.RequestUri?.ToString());
        Assert.NotNull(capturedBody);
        Assert.Contains("\"text\":\"Hello X\"", capturedBody, StringComparison.Ordinal);
        Assert.Contains("\"in_reply_to_tweet_id\":\"broadcast-1\"", capturedBody, StringComparison.Ordinal);
        Assert.True(capturedRequest.Headers.TryGetValues("Authorization", out IEnumerable<string>? values));
        Assert.StartsWith("OAuth ", Assert.Single(values), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenRateLimitHeadersPresent_ThenPollRepliesReturnsBackoffMetadata()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(
            """{"data":[],"meta":{}}""",
            remaining: 0,
            resetUnixSeconds: 1735689900));

        XApiClient client = CreateClient(handler);

        XReplyPollResult result = await client.PollReplies("broadcast-1", null);

        Assert.Equal(0, result.RateLimit.Remaining);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1735689900), result.RateLimit.ResetAt);
    }

    private static XApiClient CreateClient(HttpMessageHandler handler, XOptions? options = null)
    {
        options ??= new XOptions
        {
            ApiBaseAddress = "https://api.twitter.com/",
            ApiVersion = "2",
            BearerToken = "bearer-token"
        };

        return new XApiClient(
            Options.Create(options),
            new TestHttpClientFactory(handler));
    }

    private static HttpResponseMessage CreateJsonResponse(string json, int? remaining = null, int? resetUnixSeconds = null)
    {
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (remaining.HasValue)
        {
            response.Headers.Add("x-rate-limit-remaining", remaining.Value.ToString());
        }

        if (resetUnixSeconds.HasValue)
        {
            response.Headers.Add("x-rate-limit-reset", resetUnixSeconds.Value.ToString());
        }

        return response;
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.twitter.com/2/")
            };
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
