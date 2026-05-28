namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Starts and stops the relay ingest process that backs Thiccdal's go-live workflow.
/// </summary>
public interface IStreamingService
{
    /// <summary>
    /// Gets a value indicating whether the ingest process is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current ingest lifecycle state.
    /// </summary>
    StreamingState State { get; }

    /// <summary>
    /// Raised when the ingest lifecycle state changes.
    /// </summary>
    event EventHandler<StreamingState>? StateChanged;

    /// <summary>
    /// Starts the ingest process.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <param name="sessionId">The optional operator live-session identifier to associate with the local recording.</param>
    Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null);

    /// <summary>
    /// Stops the ingest process.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task Stop(CancellationToken cancellationToken = default);
}
