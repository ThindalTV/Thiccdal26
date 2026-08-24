namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// Read and CRUD access to the predefined overlay cards. Editing only happens in configuration.
/// </summary>
public interface IOverlayCardManagementService
{
    /// <summary>
    /// Lists every card in display order.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<IReadOnlyList<OverlayCardDefinition>> List(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new card.
    /// </summary>
    /// <param name="card">The card values.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<OverlayCardDefinition> Create(OverlayCardInput card, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing card.
    /// </summary>
    /// <param name="id">The card identifier.</param>
    /// <param name="card">The replacement values.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The updated card, or <c>null</c> when it no longer exists.</returns>
    Task<OverlayCardDefinition?> Update(long id, OverlayCardInput card, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a card.
    /// </summary>
    /// <param name="id">The card identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns><c>true</c> when the card was deleted; otherwise <c>false</c>.</returns>
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}
