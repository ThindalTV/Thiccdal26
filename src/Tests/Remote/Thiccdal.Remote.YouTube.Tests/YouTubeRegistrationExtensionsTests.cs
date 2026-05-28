using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.YouTube;
using Thiccdal.Remote.YouTube;

namespace Thiccdal.Remote.YouTube.Tests;

public sealed class YouTubeRegistrationExtensionsTests
{
    private const string OAuthClientName = "YouTubeOAuth";
    private const string ApiClientName = "YouTubeApi";

    [Fact]
    public void WhenAddYouTubePlatform_ThenRegistersAllServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IYouTubeTokenStore, InMemoryYouTubeTokenStore>();
        services.AddSingleton<IEventBus, NoOpEventBus>();

        services.AddYouTubePlatform(BuildConfiguration());

        using ServiceProvider provider = services.BuildServiceProvider();

        IYouTubePlatformConnection platformConnection = provider.GetRequiredService<IYouTubePlatformConnection>();
        IStreamInfoProvider streamInfoProvider = provider.GetRequiredService<IStreamInfoProvider>();
        Assert.IsType<YouTubeService>(platformConnection);
        Assert.Same(platformConnection, provider.GetRequiredService<IYouTubeService>());
        Assert.Same(platformConnection, provider.GetRequiredService<IPlatformConnection>());
        Assert.Same(platformConnection, provider.GetRequiredService<IChatSource>());
        Assert.Same(platformConnection, streamInfoProvider);
        Assert.NotNull(provider.GetRequiredService<IYouTubeApiClient>());
        Assert.NotNull(provider.GetRequiredService<IYouTubeConnectionMonitor>());
        Assert.NotNull(provider.GetRequiredService<IIntegrationConnectionMonitor>());
    }

    [Fact]
    public async Task WhenPlatformClientsSeeTransientFailures_ThenResilienceRetriesOAuthAndApiCalls()
    {
        ServiceCollection services = new();
        CountingHttpMessageHandler oauthHandler = new();
        CountingHttpMessageHandler apiHandler = new();

        services.AddLogging();
        services.AddSingleton<IYouTubeTokenStore, InMemoryYouTubeTokenStore>();
        services.AddSingleton<IEventBus, NoOpEventBus>();
        services.AddYouTubeIntegration(BuildConfiguration());
        services.AddHttpClient(OAuthClientName)
            .ConfigurePrimaryHttpMessageHandler(() => oauthHandler);
        services.AddHttpClient(ApiClientName)
            .ConfigurePrimaryHttpMessageHandler(() => apiHandler);

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        using HttpResponseMessage oauthResponse = await httpClientFactory.CreateClient(OAuthClientName).GetAsync("token");
        using HttpResponseMessage apiResponse = await httpClientFactory.CreateClient(ApiClientName).GetAsync("liveBroadcasts");

        Assert.Equal(HttpStatusCode.OK, oauthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);
        Assert.Equal(6, oauthHandler.AttemptCount);
        Assert.Equal(6, apiHandler.AttemptCount);
    }

    [Fact]
    public async Task WhenYouTubeApiReturnsRetryAfter_ThenResilienceUsesServerBackoff()
    {
        ServiceCollection services = new();
        RetryAfterHttpMessageHandler apiHandler = new();

        services.AddLogging();
        services.AddSingleton<IYouTubeTokenStore, InMemoryYouTubeTokenStore>();
        services.AddSingleton<IEventBus, NoOpEventBus>();
        services.AddYouTubeIntegration(BuildConfiguration());
        services.AddHttpClient(ApiClientName)
            .ConfigurePrimaryHttpMessageHandler(() => apiHandler);

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using HttpResponseMessage response = await httpClientFactory.CreateClient(ApiClientName).GetAsync("liveBroadcasts");

        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, apiHandler.AttemptCount);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(1));
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["YouTube:ClientId"] = "test-client-id",
                ["YouTube:ClientSecret"] = "test-secret",
                ["YouTube:RedirectUri"] = "https://localhost/callback",
                ["YouTube:DefaultChannelId"] = "test-channel",
                ["YouTube:OAuthBaseAddress"] = "https://accounts.google.com/o/oauth2/",
                ["YouTube:ApiBaseAddress"] = "https://www.googleapis.com/youtube/v3/",
                ["YouTube:LiveChatPollingIntervalSeconds"] = "5",
                ["YouTube:BroadcastInfoRefreshSeconds"] = "30"
            })
            .Build();
    }

    private sealed class InMemoryYouTubeTokenStore : IYouTubeTokenStore
    {
        private YouTubeStoredToken? _token;

        public Task<YouTubeStoredToken?> GetLatestToken(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_token);
        }

        public Task ReplaceToken(YouTubeStoredToken token, CancellationToken cancellationToken = default)
        {
            _token = token;
            return Task.CompletedTask;
        }

        public Task DeleteTokens(CancellationToken cancellationToken = default)
        {
            _token = null;
            return Task.CompletedTask;
        }

        public Task<bool> HasValidToken(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            bool hasToken = _token is not null && _token.ExpiresAt > utcNow;
            return Task.FromResult(hasToken);
        }
    }

    private sealed class NoOpEventBus : IEventBus
    {
        public Task Publish(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
        {
            platformEvent.PersistedRecordId = 1;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<PlatformEvent> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AttemptCount++;

            HttpStatusCode statusCode = AttemptCount < 6
                ? HttpStatusCode.BadGateway
                : HttpStatusCode.OK;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                RequestMessage = request
            });
        }
    }

    private sealed class RetryAfterHttpMessageHandler : HttpMessageHandler
    {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AttemptCount++;

            if (AttemptCount == 1)
            {
                HttpResponseMessage throttledResponse = new(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                    RequestMessage = request
                };
                throttledResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return Task.FromResult(throttledResponse);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                RequestMessage = request
            });
        }
    }
}
