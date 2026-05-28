namespace Thiccdal.Infrastructure.LmStudio;

/// <summary>
/// Represents the first completion returned by LM Studio.
/// </summary>
/// <param name="Content">The completion content.</param>
/// <param name="Model">The model that produced the completion.</param>
/// <param name="FinishReason">The finish reason reported by the server.</param>
public sealed record LmStudioChatCompletionResult(
    string Content,
    string Model,
    string FinishReason);
