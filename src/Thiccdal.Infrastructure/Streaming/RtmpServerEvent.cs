namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Represents an event emitted by the remote RTMP server over the SignalR hub.
/// </summary>
/// <param name="EventType">The type of event that occurred.</param>
/// <param name="Message">A human-readable description of the event.</param>
/// <param name="OccurredAt">The UTC instant at which the event occurred.</param>
public sealed record RtmpServerEvent(
    RtmpServerEventType EventType,
    string Message,
    DateTimeOffset OccurredAt);
