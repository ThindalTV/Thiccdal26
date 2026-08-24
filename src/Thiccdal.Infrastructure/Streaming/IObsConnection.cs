namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Maintains the obs-websocket session with OBS Studio and reports stream output state.
/// OBS runs on the same machine as Thiccdal, so this is a local, unauthenticated-by-default hop.
/// </summary>
public interface IObsConnection
{
    /// <summary>
    /// Raised when <see cref="GetState"/> would return a different snapshot.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Returns the current OBS connection and stream state.
    /// </summary>
    ObsState GetState();

    /// <summary>
    /// Opens the obs-websocket session and keeps it open, reconnecting with backoff when it drops.
    /// Does nothing when the integration is disabled.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task Connect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the obs-websocket session and stops reconnecting.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task Disconnect(CancellationToken cancellationToken = default);
}
