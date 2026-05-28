using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Persists normalized chat events before downstream consumers observe them.
/// </summary>
public interface IChatPersistenceService
{
    /// <summary>
    /// Persists the supplied chat event.
    /// </summary>
    /// <param name="chatEvent">The chat event to persist.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task that completes when persistence finishes.</returns>
    Task Persist(ChatEvent chatEvent, CancellationToken cancellationToken = default);
}
