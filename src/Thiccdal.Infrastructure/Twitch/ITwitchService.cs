using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Infrastructure.Twitch;

public interface ITwitchService : IChatSource
{
    /// <summary>Current auth and connection state.</summary>
    TwitchConnectionState ConnectionState { get; }

    /// <summary>Fired whenever the connection state changes.</summary>
    event EventHandler<TwitchConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Checks stored token presence and updates ConnectionState.
    /// Call after token operations (store, revoke) to keep state current.
    /// </summary>
    Task RefreshConnectionState(CancellationToken cancellationToken = default);
}
