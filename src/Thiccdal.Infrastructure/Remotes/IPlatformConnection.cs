namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents a platform adapter that can emit events, send chat, and participate in the connection lifecycle.
/// </summary>
public interface IPlatformConnection : IChatSource, IStreamTarget
{
    /// <summary>
    /// Gets the platform display name exposed by status and operator surfaces.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Gets the normalized connection state for this platform.
    /// </summary>
    PlatformConnectionState State { get; }

    /// <summary>
    /// Gets the last platform error message when the state is <see cref="PlatformConnectionState.Error"/>.
    /// </summary>
    string? LastError { get; }

    /// <summary>
    /// Refreshes the current connection state from the platform's underlying auth or transport state.
    /// </summary>
    Task RefreshConnectionState(CancellationToken cancellationToken = default);
}
