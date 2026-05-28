namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Represents the current ingest lifecycle.
/// </summary>
public enum StreamingState
{
    Idle,
    WaitingForIngest,
    Live,
    BrbSlate,
    Error
}
