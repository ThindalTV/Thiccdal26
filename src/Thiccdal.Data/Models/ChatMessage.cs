using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Data.Models;

public class ChatMessage
{
    public long Id { get; set; }

    public long PlatformEventId { get; set; }

    public PlatformEvent PlatformEvent { get; set; } = null!;

    public long PlatformUserId { get; set; }

    public PlatformUser PlatformUser { get; set; } = null!;

    public PlatformEventSource Source { get; set; }

    public string Content { get; set; } = string.Empty;

    public string HtmlContent { get; set; } = string.Empty;

    public string RawData { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
