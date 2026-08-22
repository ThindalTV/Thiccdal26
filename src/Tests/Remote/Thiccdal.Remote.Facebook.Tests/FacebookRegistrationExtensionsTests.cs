using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Facebook;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Remote.Facebook;

namespace Thiccdal.Remote.Facebook.Tests;

public class FacebookRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddFacebookIntegration_ThenServicesAreRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventBus, FakeEventBus>();
        var configuration = BuildConfiguration();

        services.AddFacebookIntegration(configuration);

        ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IFacebookService>());
        Assert.NotNull(provider.GetService<IFacebookGraphClient>());
        Assert.NotNull(provider.GetService<IFacebookConnectionMonitor>());
        Assert.NotNull(provider.GetService<FacebookService>());
        Assert.NotNull(provider.GetService<FacebookConnectionMonitor>());
        Assert.NotNull(provider.GetService<IHttpClientFactory>());
    }

    [Fact]
    public void WhenAddFacebookIntegration_ThenPlatformInterfacesAreRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventBus, FakeEventBus>();
        var configuration = BuildConfiguration();

        services.AddFacebookIntegration(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        var platformConnections = provider.GetServices<IPlatformConnection>().ToList();
        var chatSources = provider.GetServices<IChatSource>().ToList();
        var streamTargets = provider.GetServices<IStreamTarget>().ToList();

        Assert.Contains(platformConnections, pc => pc is FacebookService);
        Assert.Contains(chatSources, cs => cs is FacebookService);
        Assert.Contains(streamTargets, st => st is FacebookService);
    }

    [Fact]
    public void WhenPlatformUserServiceIsScoped_ThenFacebookServiceStillResolves()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IEventBus, FakeEventBus>();
        services.AddScoped<IPlatformUserService, FakePlatformUserService>();
        services.AddFacebookIntegration(BuildConfiguration());

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(provider.GetRequiredService<IFacebookService>());
    }

    [Fact]
    public async Task WhenGraphApiClientSeesTransientFailures_ThenResilienceRetriesRequests()
    {
        ServiceCollection services = new();
        CountingHttpMessageHandler handler = new();

        services.AddLogging();
        services.AddSingleton<IEventBus, FakeEventBus>();
        services.AddFacebookIntegration(BuildConfiguration());
        services.AddHttpClient("FacebookGraphApi")
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        using HttpResponseMessage response = await httpClientFactory.CreateClient("FacebookGraphApi").GetAsync("me/live_videos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, handler.AttemptCount);
    }

    private static IConfiguration BuildConfiguration()
    {
        var config = new Dictionary<string, string?>
        {
            ["Facebook:PageAccessToken"] = "test-token",
            ["Facebook:PageId"] = "123456",
            ["Facebook:AppId"] = "app-id",
            ["Facebook:AppSecret"] = "app-secret",
            ["Facebook:OAuthBaseAddress"] = "https://www.facebook.com/",
            ["Facebook:GraphApiBaseAddress"] = "https://graph.facebook.com/",
            ["Facebook:GraphApiVersion"] = "v21.0",
            ["Facebook:DefaultPrivacy"] = "EVERYONE",
            ["Facebook:PollIntervalMs"] = "5000",
            ["Facebook:ReconnectDelaySeconds"] = "30"
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

            HttpStatusCode statusCode = AttemptCount < 4
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                RequestMessage = request
            });
        }
    }

    private sealed class FakePlatformUserService : IPlatformUserService
    {
        public Task<long> Upsert(
            PlatformEventSource source,
            string platformUserId,
            string displayName,
            DateTime lastSeen,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(1L);
        }
    }
}
