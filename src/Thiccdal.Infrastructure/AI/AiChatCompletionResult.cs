namespace Thiccdal.Infrastructure.AI;

/// <summary>
/// Represents a chat-completion result returned by Thiccdal's AI boundary.
/// </summary>
/// <param name="Content">The assistant text content.</param>
/// <param name="Model">The model that produced the content.</param>
/// <param name="FinishReason">The finish reason reported by the upstream service.</param>
public sealed record AiChatCompletionResult(
    string Content,
    string Model,
    string FinishReason);
