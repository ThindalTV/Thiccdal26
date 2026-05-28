namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents one rendered activity entry for the overlay and prompter.
/// </summary>
/// <param name="Sender">The display sender name.</param>
/// <param name="Content">The plain-text content.</param>
/// <param name="HtmlContent">The rich HTML content.</param>
/// <param name="Source">The source platform.</param>
/// <param name="Type">The normalized platform event type.</param>
/// <param name="Timestamp">The time the entry occurred.</param>
/// <param name="AccentColor">The optional accent color for the entry.</param>
/// <param name="SenderColor">The optional sender color for chat entries.</param>
/// <param name="Badges">The optional chat badges for chat entries.</param>
/// <param name="Parts">The optional chat parts for chat entries.</param>
public sealed record ActivityFeedEntry(
    string Sender,
    string Content,
    string HtmlContent,
    PlatformEventSource Source,
    PlatformEventType Type,
    DateTime Timestamp,
    string AccentColor = "",
    string SenderColor = "",
    IReadOnlyList<ChatBadge>? Badges = null,
    IReadOnlyList<ChatMessagePart>? Parts = null);
