namespace Thiccdal.Infrastructure.AI;

/// <summary>
/// Represents a single message in an AI chat-completion request.
/// </summary>
/// <param name="Role">The role for the message.</param>
/// <param name="Content">The message content.</param>
public sealed record AiChatMessage(
    AiChatMessageRole Role,
    string Content);
