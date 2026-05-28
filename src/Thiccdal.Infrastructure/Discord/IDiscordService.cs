using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Infrastructure.Discord;

/// <summary>
/// Provides Discord-specific operations on top of the standard platform connection contract.
/// </summary>
public interface IDiscordService : IPlatformConnection
{
    /// <summary>
    /// Gets the current connection state of the Discord bot.
    /// </summary>
    DiscordConnectionState ConnectionState { get; }

    /// <summary>
    /// Gets the current Discord relay capability state.
    /// </summary>
    DiscordRelayStatus RelayStatus { get; }

    /// <summary>
    /// Raised when the Discord connection state changes.
    /// </summary>
    event EventHandler<DiscordConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Refreshes the connection state by checking if credentials are configured.
    /// </summary>
    new Task RefreshConnectionState(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the Discord stream relay if the platform path supports it.
    /// </summary>
    Task StartRelay(string rtmpUrl, string streamKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the Discord stream relay if it is active.
    /// </summary>
    Task StopRelay(CancellationToken cancellationToken = default);
}
