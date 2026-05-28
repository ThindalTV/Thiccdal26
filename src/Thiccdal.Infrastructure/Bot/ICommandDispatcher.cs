using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Parses normalized chat events for command triggers and coordinates command execution.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches the supplied chat event when it matches a configured command trigger.
    /// </summary>
    /// <param name="chatEvent">The normalized chat event to inspect.</param>
    /// <param name="cancellationToken">Cancels dispatch and any downstream handler work.</param>
    Task Dispatch(ChatEvent chatEvent, CancellationToken cancellationToken = default);
}
