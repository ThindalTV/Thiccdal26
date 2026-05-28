using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Provides sanitized, scoped memory for AI chatter responses.
/// </summary>
public interface IChatterMemoryService
{
    /// <summary>
    /// Builds the current memory context for a single chatter on a single platform and channel.
    /// </summary>
    /// <param name="source">The originating platform.</param>
    /// <param name="channel">The originating platform channel identifier.</param>
    /// <param name="platformUserId">The stable platform-specific chatter identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The bounded memory context, or <see langword="null"/> when no usable memory is available.</returns>
    Task<ChatterMemoryContext?> GetMemoryContext(
        PlatformEventSource source,
        string channel,
        string platformUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the derived memory context for a single chatter scope without deleting source chat history.
    /// </summary>
    /// <param name="source">The originating platform.</param>
    /// <param name="channel">The originating platform channel identifier.</param>
    /// <param name="platformUserId">The stable platform-specific chatter identifier.</param>
    /// <param name="requestedBy">The operator or system actor requesting the reset.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task Reset(
        PlatformEventSource source,
        string channel,
        string platformUserId,
        string requestedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all derived chatter memory contexts without deleting source chat history.
    /// </summary>
    /// <param name="requestedBy">The operator or system actor requesting the reset.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task ResetAll(
        string requestedBy,
        CancellationToken cancellationToken = default);
}
