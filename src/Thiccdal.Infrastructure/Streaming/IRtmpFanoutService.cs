namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Coordinates RTMP fanout startup for enabled stream targets.
/// </summary>
public interface IRtmpFanoutService
{
    /// <summary>
    /// Gets a value indicating whether RTMP fanout is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts RTMP fanout.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task StartFanout(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops RTMP fanout.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task StopFanout(CancellationToken cancellationToken = default);
}
