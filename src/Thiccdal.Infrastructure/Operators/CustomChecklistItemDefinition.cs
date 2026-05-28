namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Represents a persisted operator-managed checklist item definition.
/// </summary>
public sealed record CustomChecklistItemDefinition
{
    /// <summary>
    /// Gets the database identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the operator-facing label.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the intended display order.
    /// </summary>
    public int DisplayOrder { get; init; }

    /// <summary>
    /// Gets a value indicating whether the item is enabled in the checklist.
    /// </summary>
    public bool IsEnabled { get; init; }
}
