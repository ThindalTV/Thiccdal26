namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Describes a listener-reported ingest state transition.
/// </summary>
public sealed record RtmpIngestStateChanged
{
    /// <summary>
    /// Gets the new ingest lifecycle state.
    /// </summary>
    public StreamingState State { get; init; }

    /// <summary>
    /// Gets the normalized RTMP stream path associated with the transition.
    /// </summary>
    public string StreamPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets an operator-facing message that explains the transition.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
