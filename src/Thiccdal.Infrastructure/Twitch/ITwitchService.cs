using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Infrastructure.Twitch;

public interface ITwitchService : IPlatformConnection
{
    /// <summary>Current auth and connection state.</summary>
    TwitchConnectionState ConnectionState { get; }

    /// <summary>Gets a value indicating whether the broadcaster is currently live.</summary>
    bool IsStreamLive { get; }

    /// <summary>Gets the latest cached Twitch stream metadata snapshot.</summary>
    TwitchStreamState StreamState { get; }

    /// <summary>Fired whenever the connection state changes.</summary>
    event EventHandler<TwitchConnectionState>? ConnectionStateChanged;

    /// <summary>Fired whenever the live stream state changes.</summary>
    event EventHandler<bool>? StreamLiveStateChanged;

    /// <summary>
    /// Checks stored token presence and updates <see cref="ConnectionState"/>.
    /// Call after token operations (store, revoke) to keep auth state current without waiting on stream metadata.
    /// </summary>
    new Task RefreshConnectionState(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks Twitch stream metadata and updates <see cref="IsStreamLive"/>.
    /// </summary>
    Task RefreshStreamState(CancellationToken cancellationToken = default);
}
