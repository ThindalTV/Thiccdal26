namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a Twitch raid notification.
/// </summary>
public sealed record TwitchRaidEvent : PlatformEvent
{
    /// <summary>
    /// Gets the login name of the raiding broadcaster.
    /// </summary>
    public string RaidingChannel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of viewers included in the raid.
    /// </summary>
    public int ViewerCount { get; init; }
}
