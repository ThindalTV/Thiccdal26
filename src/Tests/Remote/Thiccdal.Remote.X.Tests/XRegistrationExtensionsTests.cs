using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.X;
using Thiccdal.Remote.X;

namespace Thiccdal.Remote.X.Tests;

public class XRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddXIntegration_ThenServicesAreRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventBus, FakeEventBus>();
        var configuration = BuildConfiguration();

        services.AddXIntegration(configuration);

        ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IXService>());
        Assert.NotNull(provider.GetService<IXApiClient>());
        Assert.NotNull(provider.GetService<IXConnectionMonitor>());
        Assert.NotNull(provider.GetService<XService>());
        Assert.NotNull(provider.GetService<XConnectionMonitor>());
    }

    [Fact]
    public void WhenAddXIntegration_ThenPlatformInterfacesAreRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventBus, FakeEventBus>();
        var configuration = BuildConfiguration();

        services.AddXIntegration(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        var platformConnections = provider.GetServices<IPlatformConnection>().ToList();
        var chatSources = provider.GetServices<IChatSource>().ToList();
        var streamTargets = provider.GetServices<IStreamTarget>().ToList();

        Assert.Contains(platformConnections, pc => pc is XService);
        Assert.Contains(chatSources, cs => cs is XService);
        Assert.Contains(streamTargets, st => st is XService);
    }

    [Fact]
    public async Task WhenApiClientSeesTransientFailures_ThenResilienceRetriesRequests()
    {
        ServiceCollection services = new();
        CountingHttpMessageHandler handler = new();

        services.AddLogging();
        services.AddSingleton<IEventBus, FakeEventBus>();
        services.AddXIntegration(BuildConfiguration());
        services.AddHttpClient("Thiccdal.Remote.X.Api")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        using HttpResponseMessage response = await httpClientFactory.CreateClient("Thiccdal.Remote.X.Api").GetAsync("tweets/search/recent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(6, handler.AttemptCount);
    }

    [Fact]
    public async Task WhenApiClientGetsRateLimited_ThenRateLimitHandlerWaitsForResetAndRetriesOnce()
    {
        ServiceCollection services = new();
        RateLimitedHttpMessageHandler handler = new();

        services.AddLogging();
        services.AddSingleton<IEventBus, FakeEventBus>();
        services.AddXIntegration(BuildConfiguration());
        services.AddHttpClient("Thiccdal.Remote.X.Api")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using HttpResponseMessage response = await httpClientFactory.CreateClient("Thiccdal.Remote.X.Api").GetAsync("tweets/search/recent");

        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.AttemptCount);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(1));
    }

    private static IConfiguration BuildConfiguration()
    {
        var config = new Dictionary<string, string?>
        {
            ["X:BearerToken"] = "test-bearer",
            ["X:ApiKey"] = "api-key",
            ["X:ApiKeySecret"] = "api-secret",
            ["X:AccessToken"] = "access-token",
            ["X:AccessTokenSecret"] = "access-secret",
            ["X:OAuthBaseAddress"] = "https://twitter.com/",
            ["X:ApiBaseAddress"] = "https://api.twitter.com/",
            ["X:ApiVersion"] = "2",
            ["X:TweetPollingIntervalSeconds"] = "15",
            ["X:PollIntervalMs"] = "16000",
            ["X:LikesPollIntervalMs"] = "30000",
            ["X:ReconnectDelaySeconds"] = "30",
            ["X:AuthorizationUrl"] = "https://developer.x.com/en/portal/dashboard"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    private class FakeEventBus : IEventBus
    {
        public Task Publish(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
        {
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

    private sealed class RateLimitedHttpMessageHandler : HttpMessageHandler
    {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AttemptCount++;

            if (AttemptCount == 1)
            {
                HttpResponseMessage rateLimitedResponse = new(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                    RequestMessage = request
                };
                rateLimitedResponse.Headers.Add("x-rate-limit-reset", (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2).ToString());
                return Task.FromResult(rateLimitedResponse);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                RequestMessage = request
            });
        }
    }
}
