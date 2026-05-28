namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Aggregates the current pre-live checklist snapshot used by the operator UI.
/// </summary>
public sealed record PreLiveChecklistState
{
    /// <summary>
    /// Gets the ordered checklist items.
    /// </summary>
    public IReadOnlyList<ChecklistItemState> Items { get; init; } = [];

    /// <summary>
    /// Gets the number of required items that remain unchecked.
    /// </summary>
    public int RequiredUncheckedCount { get; init; }

    /// <summary>
    /// Gets the number of optional items that remain unchecked.
    /// </summary>
    public int OptionalUncheckedCount { get; init; }

    /// <summary>
    /// Gets the number of checked items.
    /// </summary>
    public int CompletedCount { get; init; }

    /// <summary>
    /// Gets the total number of items.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether all required items are satisfied.
    /// </summary>
    public bool AllRequiredChecked { get; init; }
}
