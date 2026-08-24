namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a Twitch subscribe, resubscribe, or gift subscription notification.
/// </summary>
public sealed record TwitchSubscribeEvent : PlatformEvent
{
    /// <summary>
    /// Gets the subscription tier.
    /// </summary>
    public string Tier { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the subscription was gifted.
    /// </summary>
    public bool IsGift { get; init; }

    /// <summary>
    /// Gets the gifting user's Twitch identifier when the event is a gift.
    /// </summary>
    public string GifterUserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the cumulative subscription month count when provided by Twitch.
    /// </summary>
    public int? CumulativeMonths { get; init; }

    /// <summary>
    /// Gets the number of subscriptions handed out when the event is a gift batch.
    /// </summary>
    public int? GiftCount { get; init; }
}
