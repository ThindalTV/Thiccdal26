namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Represents the current runtime state for a single pre-live checklist item.
/// </summary>
public sealed record ChecklistItemState
{
    /// <summary>
    /// Gets the item definition.
    /// </summary>
    public required ChecklistItemDefinition Definition { get; init; }

    /// <summary>
    /// Gets a value indicating whether the item is currently satisfied.
    /// </summary>
    public bool IsChecked { get; init; }

    /// <summary>
    /// Gets a value indicating whether the current checked state was derived automatically.
    /// </summary>
    public bool IsAutoChecked { get; init; }

    /// <summary>
    /// Gets a value indicating whether the item is currently blocked from completion.
    /// </summary>
    public bool IsBlocked { get; init; }

    /// <summary>
    /// Gets a value indicating whether the item is currently warning the operator without blocking go live.
    /// </summary>
    public bool IsWarning { get; init; }

    /// <summary>
    /// Gets the optional runtime warning message shown to the operator.
    /// </summary>
    public string? WarningMessage { get; init; }

    /// <summary>
    /// Gets when the item was manually checked, when tracked.
    /// </summary>
    public DateTimeOffset? CheckedAt { get; init; }
}
