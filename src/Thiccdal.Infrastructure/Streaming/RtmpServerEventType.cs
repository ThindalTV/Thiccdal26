namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Identifies the type of event emitted by the remote RTMP server.
/// </summary>
public enum RtmpServerEventType
{
    /// <summary>An OBS ingest publisher connected to the RTMP server.</summary>
    IngestConnected,

    /// <summary>The OBS ingest publisher disconnected from the RTMP server.</summary>
    IngestDisconnected,

    /// <summary>The RTMP ingest encountered an error.</summary>
    IngestError,

    /// <summary>A local recording started on the RTMP server.</summary>
    RecordingStarted,

    /// <summary>A local recording ended on the RTMP server.</summary>
    RecordingEnded,

    /// <summary>An RTMP relay to a downstream platform failed.</summary>
    RelayFailed,
}
