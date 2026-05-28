namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a reaction added to a message on a platform.
/// Currently used by Discord; may be extended to other platforms that support reactions.
/// </summary>
public record ReactionEvent : PlatformEvent
{
    /// <summary>
    /// Gets the name of the emote/emoji that was used for the reaction.
    /// For standard Unicode emoji, this is the emoji character itself.
    /// For custom emotes, this is the emote name.
    /// </summary>
    public required string EmoteName { get; init; }

    /// <summary>
    /// Gets the ID of the custom emote, if applicable.
    /// Null for standard Unicode emoji.
    /// </summary>
    public string? EmoteId { get; init; }

    /// <summary>
    /// Gets the ID of the message that was reacted to.
    /// Platform-specific identifier (e.g., Discord snowflake).
    /// </summary>
    public required string MessageId { get; init; }
}
