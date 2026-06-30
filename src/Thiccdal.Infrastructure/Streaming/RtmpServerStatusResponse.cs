namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Represents the current runtime status of the remote RTMP server.
/// </summary>
/// <param name="IsIngestRunning">Whether the RTMP ingest listener is active.</param>
/// <param name="IsFanoutRunning">Whether the relay fanout is active.</param>
/// <param name="IsRecording">Whether local disk recording is in progress.</param>
/// <param name="IngestState">A human-readable description of the current ingest state.</param>
/// <param name="ActiveRelayCount">The number of relay sessions currently running.</param>
/// <param name="ErrorMessage">Non-empty when the response represents a communication error.</param>
public sealed record RtmpServerStatusResponse(
    bool IsIngestRunning,
    bool IsFanoutRunning,
    bool IsRecording,
    string IngestState,
    int ActiveRelayCount,
    string ErrorMessage);
