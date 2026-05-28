namespace Thiccdal.Infrastructure.LmStudio;

/// <summary>
/// Represents a single chat message in an LM Studio request.
/// </summary>
/// <param name="Role">The OpenAI-compatible chat role.</param>
/// <param name="Content">The message content.</param>
public sealed record LmStudioChatMessage(
    string Role,
    string Content);
