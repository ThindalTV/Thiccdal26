namespace Thiccdal.Infrastructure.Bot.Models;

public record ChatEvent : PlatformEvent
{
    public required string Content { get; init; }
}
