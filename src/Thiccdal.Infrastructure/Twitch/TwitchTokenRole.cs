namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Identifies which Twitch account a stored authorization belongs to.
/// Twitch grants channel-level reads only to the channel owner, so the bot account that speaks in
/// chat cannot subscribe to subscriptions, cheers, or redemptions on the broadcaster's behalf.
/// </summary>
public enum TwitchTokenRole
{
    /// <summary>
    /// The account that reads and writes chat.
    /// </summary>
    Bot = 0,

    /// <summary>
    /// The channel owner, whose authorization carries the channel-level read scopes.
    /// </summary>
    Broadcaster = 1
}
