namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Manages operator-defined checklist items persisted for the Personal Prep checklist category.
/// </summary>
public interface ICustomChecklistItemManagementService
{
    /// <summary>
    /// Lists all custom checklist items in display order.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted custom checklist items.</returns>
    Task<IReadOnlyList<CustomChecklistItemDefinition>> List(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new custom checklist item at the end of the current ordered list.
    /// </summary>
    /// <param name="label">The operator-facing label.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created custom checklist item.</returns>
    Task<CustomChecklistItemDefinition> Create(string label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing custom checklist item.
    /// </summary>
    /// <param name="item">The item values to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated item, or <c>null</c> when the item no longer exists.</returns>
    Task<CustomChecklistItemDefinition?> Update(CustomChecklistItemDefinition item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an existing custom checklist item.
    /// </summary>
    /// <param name="id">The database identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the item was deleted; otherwise <c>false</c>.</returns>
    Task<bool> Delete(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an existing custom checklist item one step earlier in display order.
    /// </summary>
    /// <param name="id">The database identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the item moved; otherwise <c>false</c>.</returns>
    Task<bool> MoveUp(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an existing custom checklist item one step later in display order.
    /// </summary>
    /// <param name="id">The database identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the item moved; otherwise <c>false</c>.</returns>
    Task<bool> MoveDown(int id, CancellationToken cancellationToken = default);
}
