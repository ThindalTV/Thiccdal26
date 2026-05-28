using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Data.Models;

/// <summary>
/// Represents a persisted platform event row in the shared TPH event table.
/// </summary>
public class PlatformEvent
{
    public long Id { get; set; }

    public PlatformEventSource Source { get; set; }

    public PlatformEventType Type { get; set; }

    public string SourceEventType { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string HtmlContent { get; set; } = string.Empty;

    public string RawData { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
