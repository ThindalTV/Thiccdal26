using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Questions;

/// <summary>
/// Locates queue-worthy viewer questions from normalized chat events.
/// </summary>
public interface IQuestionLocatorService
{
    /// <summary>
    /// Returns the normalized question text when the chat event should be queued.
    /// </summary>
    /// <param name="chatEvent">The chat event to inspect.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The normalized question text, or <see langword="null"/> when no question was found.</returns>
    Task<string?> TryLocateQuestion(ChatEvent chatEvent, CancellationToken cancellationToken = default);
}
