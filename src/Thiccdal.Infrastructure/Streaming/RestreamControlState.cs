namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Snapshot of the operator-facing restream configuration and runtime state.
/// </summary>
public sealed record RestreamControlState
{
    /// <summary>
    /// Gets the configured RTMP ingest URL.
    /// </summary>
    public string IngestUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the configured recording output path.
    /// </summary>
    public string RecordingOutputPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional BRB slate media path reserved for ingest disconnect recovery.
    /// </summary>
    public string BrbSlatePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether restreaming should start automatically with the host.
    /// </summary>
    public bool StartWithHost { get; init; }

    /// <summary>
    /// Gets a value indicating whether a BRB slate path has been configured.
    /// </summary>
    public bool IsBrbSlateConfigured { get; init; }

    /// <summary>
    /// Gets a value indicating whether ingest is currently marked as running.
    /// </summary>
    public bool IsIngestRunning { get; init; }

    /// <summary>
    /// Gets a value indicating whether fanout is currently marked as running.
    /// </summary>
    public bool IsFanoutRunning { get; init; }

    /// <summary>
    /// Gets the number of enabled restream destinations.
    /// </summary>
    public int EnabledDestinationCount { get; init; }

    /// <summary>
    /// Gets the number of connected platform destinations.
    /// </summary>
    public int ConnectedDestinationCount { get; init; }

    /// <summary>
    /// Gets the number of enabled destinations that are currently connected.
    /// </summary>
    public int ActiveDestinationCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the runtime can be started safely from the current state.
    /// </summary>
    public bool CanStart { get; init; }

    /// <summary>
    /// Gets an operator-facing message describing the last action outcome.
    /// </summary>
    public string OperatorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets a note describing the current backend seam for restreaming.
    /// </summary>
    public string DependencyNote { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether local disk recording is currently active.
    /// </summary>
    public bool IsRecording { get; init; }

    /// <summary>
    /// Gets the latest persisted local recording row when available.
    /// </summary>
    public StreamRecordingSnapshot? LatestRecording { get; init; }

    /// <summary>
    /// Gets the current per-platform restream destination state.
    /// </summary>
    public IReadOnlyList<RestreamDestinationState> Destinations { get; init; } = Array.Empty<RestreamDestinationState>();

    /// <summary>Gets a value indicating whether the remote RTMP server SignalR hub is currently reachable.</summary>
    public bool IsRtmpServerConnected { get; init; }

    /// <summary>Gets the configured RTMP server base URL.</summary>
    public string RtmpServerUrl { get; init; } = string.Empty;
}
