namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Snapshot of the current ingest seam.
/// </summary>
public sealed record StreamingProcessState
{
    /// <summary>
    /// Gets a value indicating whether ingest is currently marked as running.
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// Gets the current ingest lifecycle state.
    /// </summary>
    public StreamingState State { get; init; }

    /// <summary>
    /// Gets the configured ingest URL currently used by the seam.
    /// </summary>
    public string IngestUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the normalized RTMP stream path currently expected by the listener.
    /// </summary>
    public string StreamPath { get; init; } = string.Empty;
}
