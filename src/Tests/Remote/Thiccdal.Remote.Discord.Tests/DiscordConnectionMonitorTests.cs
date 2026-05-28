using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Discord;

namespace Thiccdal.Remote.Discord.Tests;

public class DiscordConnectionMonitorTests
{
    [Fact]
    public void WhenDiscordOptionsAreEmpty_ThenIsConnectedIsFalse()
    {
        var options = new DiscordOptions();
        var monitor = new DiscordConnectionMonitor(
            Options.Create(options),
            NullLogger<DiscordConnectionMonitor>.Instance);

        monitor.RefreshConnectionState();

        Assert.False(monitor.IsConnected);
    }

    [Fact]
    public void WhenAllDiscordOptionsAreSet_ThenIsConnectedIsTrue()
    {
        var options = new DiscordOptions
        {
            BotToken = "test-token",
            GuildId = "123456789",
            StreamChannelId = "987654321"
        };
        var monitor = new DiscordConnectionMonitor(
            Options.Create(options),
            NullLogger<DiscordConnectionMonitor>.Instance);

        monitor.RefreshConnectionState();

        Assert.True(monitor.IsConnected);
    }

    [Fact]
    public void WhenConnectionStateChanges_ThenConnectionChangedEventIsRaised()
    {
        var options = new DiscordOptions
        {
            BotToken = "test-token",
            GuildId = "123456789",
            StreamChannelId = "987654321"
        };
        var monitor = new DiscordConnectionMonitor(
            Options.Create(options),
            NullLogger<DiscordConnectionMonitor>.Instance);

        bool eventRaised = false;
        monitor.ConnectionChanged += (sender, args) => eventRaised = true;

        monitor.RefreshConnectionState();

        Assert.True(eventRaised);
    }

    [Fact]
    public void WhenPlatformName_ThenReturnsDiscord()
    {
        var options = new DiscordOptions();
        var monitor = new DiscordConnectionMonitor(
            Options.Create(options),
            NullLogger<DiscordConnectionMonitor>.Instance);

        Assert.Equal("Discord", monitor.PlatformName);
    }

    [Fact]
    public void WhenRelayStatusRequested_ThenDiscordRelayIsMarkedBlocked()
    {
        var options = new DiscordOptions
        {
            VoiceChannelId = "123456789"
        };
        var monitor = new DiscordConnectionMonitor(
            Options.Create(options),
            NullLogger<DiscordConnectionMonitor>.Instance);

        monitor.RefreshConnectionState();

        Assert.False(monitor.RelayStatus.IsSupported);
        Assert.Equal(DiscordRelaySupport.BlockedReason, monitor.RelayStatus.StatusMessage);
    }
}
