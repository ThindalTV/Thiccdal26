namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Lists operator-managed checklist items persisted outside the runtime checklist service.
/// </summary>
public interface ICustomChecklistItemCatalog
{
    /// <summary>
    /// Lists custom checklist items in persisted order.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current catalog of custom checklist items.</returns>
    Task<IReadOnlyList<CustomChecklistItemDefinition>> List(CancellationToken cancellationToken = default);
}
