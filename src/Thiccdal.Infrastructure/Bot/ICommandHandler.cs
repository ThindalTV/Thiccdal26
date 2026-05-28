namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Handles a configured chatbot command invocation.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Handles a command invocation and optionally returns a chat response.
    /// Returning <see langword="null"/> suppresses the static template response because the handler owns output itself.
    /// </summary>
    /// <param name="context">The normalized invocation context.</param>
    /// <param name="cancellationToken">Cancels the handler operation.</param>
    /// <returns>The response to send to chat, or <see langword="null"/> to suppress the static template.</returns>
    Task<string?> Handle(CommandContext context, CancellationToken cancellationToken = default);
}
