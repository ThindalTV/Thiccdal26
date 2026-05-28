using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Discord;

namespace Thiccdal.Remote.Discord;

/// <summary>
/// Singleton that tracks whether Discord is configured and ready to connect.
/// </summary>
public sealed class DiscordConnectionMonitor : IDiscordConnectionMonitor
{
    private readonly DiscordOptions _options;
    private readonly ILogger<DiscordConnectionMonitor> _logger;
    private bool _relayWarningLogged;

    public string PlatformName => "Discord";

    public bool IsConnected { get; private set; }
    public DiscordRelayStatus RelayStatus => DiscordRelaySupport.BlockedStatus;

    public event EventHandler? ConnectionChanged;

    public DiscordConnectionMonitor(
        IOptions<DiscordOptions> options,
        ILogger<DiscordConnectionMonitor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GetAuthorizationUrl()
    {
        return string.Empty;
    }

    public Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        LogRelayBlockedWarningIfConfigured();

        var hasValidConfig = !string.IsNullOrWhiteSpace(_options.BotToken) &&
                            !string.IsNullOrWhiteSpace(_options.GuildId) &&
                            !string.IsNullOrWhiteSpace(_options.StreamChannelId);

        var wasConnected = IsConnected;
        IsConnected = hasValidConfig;

        if (wasConnected != IsConnected)
        {
            _logger.LogInformation(
                "Discord connection state changed to {State}",
                IsConnected ? "connected" : "disconnected");

            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    private void LogRelayBlockedWarningIfConfigured()
    {
        if (_relayWarningLogged || string.IsNullOrWhiteSpace(_options.VoiceChannelId))
        {
            return;
        }

        _relayWarningLogged = true;
        _logger.LogWarning(
            "Discord voice channel {ChannelId} is configured, but relay remains blocked: {Reason}",
            _options.VoiceChannelId,
            RelayStatus.StatusMessage);
    }
}
