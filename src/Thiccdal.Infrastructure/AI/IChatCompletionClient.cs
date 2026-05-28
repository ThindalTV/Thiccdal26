namespace Thiccdal.Infrastructure.AI;

/// <summary>
/// Sends chat-completion requests through Thiccdal's AI boundary.
/// </summary>
public interface IChatCompletionClient
{
    /// <summary>
    /// Completes a chat request.
    /// </summary>
    /// <param name="request">The request to submit.</param>
    /// <param name="cancellationToken">Cancels the outbound request.</param>
    /// <returns>The first completion returned by the upstream model.</returns>
    Task<AiChatCompletionResult> CompleteChat(
        AiChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
