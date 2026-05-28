namespace Thiccdal.Infrastructure.LmStudio;

/// <summary>
/// Sends chat-completion requests to an LM Studio server.
/// </summary>
public interface ILmStudioClient
{
    /// <summary>
    /// Sends a chat-completion request.
    /// </summary>
    /// <param name="request">The request to submit.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The first completion returned by LM Studio.</returns>
    Task<LmStudioChatCompletionResult> CompleteChat(
        LmStudioChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
