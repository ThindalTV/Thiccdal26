using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Thiccdal.Infrastructure.Discord;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Remote.Discord.Tests;

public class DiscordRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddDiscordIntegration_ThenAllServicesAreRegistered()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:BotToken"] = "test-token",
                ["Discord:GuildId"] = "123456789",
                ["Discord:StreamChannelId"] = "987654321",
                ["Discord:ReconnectDelaySeconds"] = "5"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IEventBus>());
        services.AddDiscordIntegration(configuration);

        var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<DiscordService>());
        Assert.NotNull(serviceProvider.GetService<IDiscordService>());
        Assert.NotNull(serviceProvider.GetService<IPlatformConnection>());
        Assert.NotNull(serviceProvider.GetService<IChatSource>());
        Assert.NotNull(serviceProvider.GetService<IStreamTarget>());
        Assert.NotNull(serviceProvider.GetService<IEventSource>());
        Assert.NotNull(serviceProvider.GetService<IDiscordConnectionMonitor>());
        Assert.NotNull(serviceProvider.GetService<IIntegrationConnectionMonitor>());
    }

    [Fact]
    public void WhenAddDiscordIntegration_ThenOptionsAreBound()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:BotToken"] = "my-bot-token",
                ["Discord:GuildId"] = "111222333",
                ["Discord:StreamChannelId"] = "444555666",
                ["Discord:VoiceChannelId"] = "777888999",
                ["Discord:ReconnectDelaySeconds"] = "10"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IEventBus>());
        services.AddDiscordIntegration(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DiscordOptions>>().Value;

        Assert.Equal("my-bot-token", options.BotToken);
        Assert.Equal("111222333", options.GuildId);
        Assert.Equal("444555666", options.StreamChannelId);
        Assert.Equal("777888999", options.VoiceChannelId);
        Assert.Equal(10, options.ReconnectDelaySeconds);
    }
}
