namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Resolved Twitch chat connection profile combining the bot identity with the current target channel.
/// </summary>
public sealed record TwitchChatConnectionProfile
{
    /// <summary>
    /// Gets the Twitch login name for the authenticated bot account.
    /// </summary>
    public required string BotUsername { get; init; }

    /// <summary>
    /// Gets the Twitch numeric user ID for the authenticated bot account.
    /// </summary>
    public string BotUserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Twitch login name for the channel the bot should join.
    /// </summary>
    public required string TargetChannel { get; init; }

    /// <summary>
    /// Gets the Twitch numeric user ID for the target broadcaster/channel owner.
    /// </summary>
    public string BroadcasterId { get; init; } = string.Empty;
}
