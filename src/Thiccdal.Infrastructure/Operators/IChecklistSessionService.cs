namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Persists audit snapshots for pre-live checklist sessions.
/// </summary>
public interface IChecklistSessionService
{
    /// <summary>
    /// Saves the current checklist snapshot for the supplied stream session.
    /// </summary>
    /// <param name="sessionId">The stream session identifier.</param>
    /// <param name="checklist">The checklist service supplying the current snapshot.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task that completes when the snapshot is persisted.</returns>
    Task Save(Guid sessionId, IPreLiveChecklistService checklist, CancellationToken cancellationToken = default);
}
