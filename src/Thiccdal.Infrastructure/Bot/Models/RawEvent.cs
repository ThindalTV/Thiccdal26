namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a source event that could not be mapped to a richer normalized type.
/// </summary>
public record RawEvent : PlatformEvent;
