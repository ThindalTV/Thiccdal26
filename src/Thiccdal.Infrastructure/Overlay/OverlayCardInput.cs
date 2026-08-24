namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// The editable values of a predefined overlay card.
/// </summary>
public sealed record OverlayCardInput
{
    public string Category { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string AccentColor { get; init; } = "#9146FF";

    public int SortOrder { get; init; }

    public bool IsEnabled { get; init; } = true;
}
