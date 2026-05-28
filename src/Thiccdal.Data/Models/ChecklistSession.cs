namespace Thiccdal.Data.Models;

/// <summary>
/// Represents the pre-live checklist snapshot captured for a stream session.
/// </summary>
public sealed class ChecklistSession
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public ICollection<ChecklistSessionItem> Items { get; set; } = [];
}
