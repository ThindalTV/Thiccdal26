namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Sends a chatbot command response to the appropriate chat surface for the originating request.
/// </summary>
public interface ICommandResponseSink
{
    /// <summary>
    /// Sends the response for the supplied command invocation context.
    /// </summary>
    /// <param name="context">The originating command context.</param>
    /// <param name="response">The message to emit.</param>
    /// <param name="cancellationToken">Cancels the send operation.</param>
    Task SendResponse(CommandContext context, string response, CancellationToken cancellationToken = default);
}
