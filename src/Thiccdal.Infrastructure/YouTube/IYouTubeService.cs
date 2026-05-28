using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Infrastructure.YouTube;

public interface IYouTubeService : IYouTubePlatformConnection
{
    /// <summary>Current auth and connection state.</summary>
    YouTubeConnectionState ConnectionState { get; }

    /// <summary>Gets a value indicating whether the channel is currently live.</summary>
    bool IsStreamLive { get; }

    /// <summary>Gets the latest cached broadcast metadata snapshot.</summary>
    YouTubeBroadcastInfo? ActiveBroadcast { get; }

    /// <summary>Fired whenever the connection state changes.</summary>
    event EventHandler<YouTubeConnectionState>? ConnectionStateChanged;

    /// <summary>Fired whenever the live stream state changes.</summary>
    event EventHandler<bool>? StreamLiveStateChanged;

    /// <summary>
    /// Checks stored token presence and updates <see cref="ConnectionState"/>.
    /// Call after token operations (store, revoke) to keep auth state current without waiting on stream metadata.
    /// </summary>
    new Task RefreshConnectionState(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks YouTube broadcast metadata and updates <see cref="IsStreamLive"/>.
    /// </summary>
    Task RefreshStreamState(CancellationToken cancellationToken = default);
}
