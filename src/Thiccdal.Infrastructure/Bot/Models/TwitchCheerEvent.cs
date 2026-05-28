namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a Twitch cheer notification.
/// </summary>
public sealed record TwitchCheerEvent : PlatformEvent
{
    /// <summary>
    /// Gets the number of bits cheered.
    /// </summary>
    public int Bits { get; init; }

    /// <summary>
    /// Gets the optional chat message that accompanied the cheer.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
