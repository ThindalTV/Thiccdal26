using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Persists normalized platform events before downstream consumers observe them.
/// </summary>
public interface IEventPersistenceService
{
    /// <summary>
    /// Persists the supplied platform event.
    /// </summary>
    /// <param name="platformEvent">The event to persist.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task that completes when persistence finishes.</returns>
    Task Persist(PlatformEvent platformEvent, CancellationToken cancellationToken = default);
}
