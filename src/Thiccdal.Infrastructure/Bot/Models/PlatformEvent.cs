namespace Thiccdal.Infrastructure.Bot.Models;

public record PlatformEvent
{
    public required PlatformEventSource Source { get; init; }

    public required string Author { get; init; }

    public required string Channel { get; init; }
}
