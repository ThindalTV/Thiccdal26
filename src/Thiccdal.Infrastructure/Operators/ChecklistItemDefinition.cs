namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Defines a single pre-live checklist item exposed to the operator UI.
/// </summary>
public sealed record ChecklistItemDefinition
{
    /// <summary>
    /// Gets the stable identifier for the item.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the category heading used to group the item.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the operator-facing label.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the satisfaction mechanism for the item.
    /// </summary>
    public ChecklistItemType Type { get; init; }

    /// <summary>
    /// Gets a value indicating whether the item blocks going live.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the optional secondary instruction shown alongside the item.
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Gets the optional label to use for an action trigger.
    /// </summary>
    public string? ActionLabel { get; init; }

    /// <summary>
    /// Gets the optional inline value rendered with the item when the operator needs reference data.
    /// </summary>
    public string? InlineValue { get; init; }

    /// <summary>
    /// Gets a value indicating whether the inline value can be copied directly from the checklist UI.
    /// </summary>
    public bool CanCopyInlineValue { get; init; }

    /// <summary>
    /// Gets the stable display order.
    /// </summary>
    public int SortOrder { get; init; }
}
