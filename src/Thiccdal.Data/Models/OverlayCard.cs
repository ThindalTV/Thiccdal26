namespace Thiccdal.Data.Models;

/// <summary>
/// A predefined overlay card the operator pushes onto the lower third from the dashboard.
/// </summary>
public sealed class OverlayCard
{
    public long Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string AccentColor { get; set; } = "#9146FF";

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;
}
