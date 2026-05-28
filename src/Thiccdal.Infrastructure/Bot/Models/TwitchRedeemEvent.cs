namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a Twitch channel-points redemption notification.
/// </summary>
public sealed record TwitchRedeemEvent : PlatformEvent
{
    /// <summary>
    /// Gets the redeemed reward identifier.
    /// </summary>
    public string RewardId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the reward title.
    /// </summary>
    public string RewardTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets any user input attached to the reward redemption.
    /// </summary>
    public string UserInput { get; init; } = string.Empty;
}
