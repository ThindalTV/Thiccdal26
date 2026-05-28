namespace Thiccdal.Infrastructure.AI;

/// <summary>
/// Identifies the role of a chat message sent to an AI model.
/// </summary>
public enum AiChatMessageRole
{
    /// <summary>
    /// Represents system-level instructions.
    /// </summary>
    System,

    /// <summary>
    /// Represents end-user input.
    /// </summary>
    User,

    /// <summary>
    /// Represents assistant output used as context.
    /// </summary>
    Assistant
}
