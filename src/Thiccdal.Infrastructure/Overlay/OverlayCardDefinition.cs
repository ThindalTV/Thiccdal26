namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// A predefined overlay card the operator can push onto the lower third with one tap.
/// </summary>
/// <param name="Id">The database identifier.</param>
/// <param name="Category">The small label above the card copy.</param>
/// <param name="Title">The card headline.</param>
/// <param name="Description">The supporting line shown under the headline.</param>
/// <param name="AccentColor">The CSS colour used to tint the card and the overlay.</param>
/// <param name="SortOrder">The position within the card list.</param>
/// <param name="IsEnabled">Whether the card can be pushed.</param>
public sealed record OverlayCardDefinition(
    long Id,
    string Category,
    string Title,
    string Description,
    string AccentColor,
    int SortOrder,
    bool IsEnabled);
