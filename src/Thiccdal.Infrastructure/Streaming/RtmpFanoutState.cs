namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Snapshot of the current RTMP fanout seam.
/// </summary>
public sealed record RtmpFanoutState
{
    /// <summary>
    /// Gets a value indicating whether fanout is currently marked as running.
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// Gets the number of registered stream targets.
    /// </summary>
    public int TargetCount { get; init; }
}
