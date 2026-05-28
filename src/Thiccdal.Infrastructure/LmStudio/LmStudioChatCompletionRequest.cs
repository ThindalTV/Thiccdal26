namespace Thiccdal.Infrastructure.LmStudio;

/// <summary>
/// Represents an LM Studio chat-completion request.
/// </summary>
/// <param name="Model">The model identifier to use.</param>
/// <param name="Messages">The ordered chat messages to submit.</param>
/// <param name="Temperature">The optional sampling temperature.</param>
/// <param name="MaxTokens">The optional maximum token count.</param>
public sealed record LmStudioChatCompletionRequest(
    string Model,
    IReadOnlyList<LmStudioChatMessage> Messages,
    double? Temperature = null,
    int? MaxTokens = null);
