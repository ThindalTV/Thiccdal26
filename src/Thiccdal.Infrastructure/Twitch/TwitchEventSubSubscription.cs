namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Represents an EventSub subscription known to Twitch Helix.
/// </summary>
public sealed record TwitchEventSubSubscription
{
    /// <summary>
    /// Gets the Twitch subscription identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the subscription type.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets the subscription version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the normalized condition map for the subscription.
    /// </summary>
    public IReadOnlyDictionary<string, string> Condition { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the WebSocket session ID this subscription is bound to, or empty for webhook subscriptions.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}
