namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Coordinates local disk recording for the active ingest session.
/// </summary>
public interface IDiskRecorder
{
    /// <summary>
    /// Gets a value indicating whether the disk recorder is actively capturing output.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Starts disk recording for the current ingest session.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <param name="sessionId">The optional operator live-session identifier to associate with the recording row.</param>
    Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null);

    /// <summary>
    /// Stops the current disk recording if one is active.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task Stop(CancellationToken cancellationToken = default);
}
