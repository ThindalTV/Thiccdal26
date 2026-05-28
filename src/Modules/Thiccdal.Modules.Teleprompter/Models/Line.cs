using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Modules.Teleprompter.Models;

public record Line(
    string Sender,
    string Content,
    string HtmlContent,
    string Platform,
    DateTime Timestamp,
    PlatformEventType Type = PlatformEventType.ChatMessage,
    string? AccentColor = null,
    string? SenderColor = null,
    IReadOnlyList<ChatBadge>? Badges = null,
    IReadOnlyList<ChatMessagePart>? Parts = null);
