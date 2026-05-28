namespace Thiccdal.Data.Models;

/// <summary>
/// Represents a single checklist item captured in a persisted session snapshot.
/// </summary>
public sealed class ChecklistSessionItem
{
    public long Id { get; set; }

    public long ChecklistSessionId { get; set; }

    public ChecklistSession ChecklistSession { get; set; } = null!;

    public string ItemId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public string? WarningMessage { get; set; }
}
