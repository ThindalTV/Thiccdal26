namespace Thiccdal.Infrastructure.Bot.Models;

public record RawEvent : PlatformEvent
{
    public required string RawData { get; init; }
}
