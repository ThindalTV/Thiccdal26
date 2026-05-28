using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Provides independent subscriber streams for normalized chat across all connected platforms.
/// </summary>
public interface IChatAggregationService
{
    /// <summary>
    /// Subscribes to the aggregated chat stream.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the subscription.</param>
    /// <returns>An asynchronous stream of chat events.</returns>
    IAsyncEnumerable<ChatEvent> Subscribe(CancellationToken cancellationToken = default);
}
