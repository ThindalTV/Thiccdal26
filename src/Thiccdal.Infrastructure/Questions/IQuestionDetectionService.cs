namespace Thiccdal.Infrastructure.Questions;

/// <summary>
/// Determines whether incoming chat text should be added to the viewer question queue.
/// </summary>
public interface IQuestionDetectionService
{
    /// <summary>
    /// Evaluates whether the supplied message should be treated as a queue-worthy question.
    /// </summary>
    /// <param name="message">The normalized chat message to inspect.</param>
    /// <param name="cancellationToken">Cancels the remote evaluation.</param>
    /// <returns><see langword="true"/> when the message should be queued; otherwise <see langword="false"/>.</returns>
    Task<bool> IsQuestion(string message, CancellationToken cancellationToken = default);
}
