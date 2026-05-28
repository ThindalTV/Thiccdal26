namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a normalized chat message with platform-agnostic rich message parts.
/// </summary>
public record ChatEvent : PlatformEvent
{
    /// <summary>
    /// Gets the stable platform-specific user identifier for the chatter when the adapter can provide one.
    /// </summary>
    public string PlatformUserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the canonical author name for UI rendering when identity merge data is available.
    /// </summary>
    public string PreferredAuthor { get; set; } = string.Empty;

    /// <summary>
    /// Gets the author name that UI renderers should display.
    /// </summary>
    public string DisplayAuthor => string.IsNullOrWhiteSpace(PreferredAuthor) ? Author : PreferredAuthor;

    /// <summary>
    /// Gets the plain-text fallback content for the chat message.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets pre-rendered HTML for consumers that support inline rich content.
    /// </summary>
    public string HtmlContent { get; init; } = string.Empty;

    /// <summary>
    /// Gets the sender's display color when provided by the platform.
    /// </summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>
    /// Gets normalized message parts such as text runs and images.
    /// </summary>
    public IReadOnlyList<ChatMessagePart> Parts { get; init; } = [];

    /// <summary>
    /// Gets the sender badges that accompanied the message.
    /// </summary>
    public IReadOnlyList<ChatBadge> Badges { get; init; } = [];
}
