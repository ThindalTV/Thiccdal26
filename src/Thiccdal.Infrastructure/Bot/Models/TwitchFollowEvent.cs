namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a Twitch follow notification.
/// </summary>
public sealed record TwitchFollowEvent : PlatformEvent
{
    /// <summary>
    /// Gets the follower's Twitch user identifier.
    /// </summary>
    public string FollowerUserId { get; init; } = string.Empty;
}
