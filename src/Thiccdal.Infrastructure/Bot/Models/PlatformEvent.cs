namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a normalized platform event that can be consumed by modules without depending on vendor payload shapes.
/// </summary>
public record PlatformEvent
{
    /// <summary>
    /// Gets the database identifier assigned when the event is persisted.
    /// </summary>
    public long PersistedRecordId { get; set; }

    /// <summary>
    /// Gets the platform that emitted the event.
    /// </summary>
    public required PlatformEventSource Source { get; init; }

    /// <summary>
    /// Gets the normalized event type.
    /// </summary>
    public required PlatformEventType Type { get; init; }

    /// <summary>
    /// Gets the source-platform event type or message kind when the adapter can surface it.
    /// </summary>
    public string SourceEventType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the author or actor that triggered the event.
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// Gets the target channel for the event.
    /// </summary>
    public required string Channel { get; init; }

    /// <summary>
    /// Gets an external identifier for the event when the source platform provides one.
    /// </summary>
    public string ExternalId { get; init; } = string.Empty;

    /// <summary>
    /// Gets a short human-readable summary of the event.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the time the platform reported the event occurred.
    /// </summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the raw vendor payload for diagnostics and downstream enrichment.
    /// </summary>
    public string RawData { get; init; } = string.Empty;
}
