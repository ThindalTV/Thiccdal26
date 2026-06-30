using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Instagram;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Remote.Instagram;

namespace Thiccdal.Remote.Instagram.Tests;

public class InstagramRegistrationExtensionsTests
{
    [Fact]
    public void WhenInstagramIntegrationRegistered_ThenInstagramServiceResolves()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{InstagramOptions.SectionName}:IsEnabled"] = "false",
                [$"{InstagramOptions.SectionName}:BroadcasterId"] = "test-broadcaster-123",
                [$"{InstagramOptions.SectionName}:AccessToken"] = ""
            })
            .Build();

        services.AddInstagramIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        var instagramConnection = provider.GetRequiredService<InstagramService>();
        var platformConnection = provider.GetRequiredService<IPlatformConnection>();
        var chatSource = provider.GetRequiredService<IChatSource>();
        var streamTarget = provider.GetRequiredService<IStreamTarget>();
        var eventSource = provider.GetRequiredService<IEventSource>();
        var platformEventSource = provider.GetRequiredService<IPlatformEventSource>();
        var integrationMonitor = provider.GetRequiredService<IIntegrationConnectionMonitor>();
        var options = provider.GetRequiredService<IOptions<InstagramOptions>>().Value;

        Assert.Same(instagramConnection, platformConnection);
        Assert.Same(instagramConnection, chatSource);
        Assert.Same(instagramConnection, streamTarget);
        Assert.Same(instagramConnection, eventSource);
        Assert.Same(instagramConnection, platformEventSource);
        Assert.NotSame(instagramConnection, integrationMonitor);
        Assert.False(options.IsEnabled);
        Assert.Equal("test-broadcaster-123", options.BroadcasterId);
    }

    [Fact]
    public void WhenInstagramDisabled_ThenStateIsPendingApproval()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.ClearProviders());
        var service = new InstagramService(
            Options.Create(new InstagramOptions
            {
                IsEnabled = false,
                BroadcasterId = "test-broadcaster-123",
                AccessToken = ""
            }),
            loggerFactory.CreateLogger<InstagramService>());

        Assert.Equal(PlatformConnectionState.PendingApproval, service.State);
        Assert.False(service.Connected);
    }

    [Fact]
    public async Task WhenInstagramDisabled_ThenRtmpDestinationIsNull()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.ClearProviders());
        var service = new InstagramService(
            Options.Create(new InstagramOptions
            {
                IsEnabled = false,
                BroadcasterId = "test-broadcaster-123",
                AccessToken = "",
                RtmpServerUrl = "",
                StreamKey = ""
            }),
            loggerFactory.CreateLogger<InstagramService>());

        RtmpRelayDestination? destination = await service.GetRelayDestination();

        Assert.Null(destination);
    }
}
