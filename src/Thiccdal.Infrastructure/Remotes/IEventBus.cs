using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Publishes persisted platform events to in-process subscribers.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Persists the event and then publishes it to all subscribers.
    /// </summary>
    /// <param name="platformEvent">The event to publish.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task that completes when the event has been persisted and fanned out.</returns>
    Task Publish(PlatformEvent platformEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to all published platform events.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that ends the subscription.</param>
    /// <returns>An asynchronous stream of published platform events.</returns>
    IAsyncEnumerable<PlatformEvent> Subscribe(CancellationToken cancellationToken = default);
}
