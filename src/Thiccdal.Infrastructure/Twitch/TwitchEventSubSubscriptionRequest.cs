namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Represents a request to create a Twitch EventSub subscription for a WebSocket session.
/// </summary>
public sealed record TwitchEventSubSubscriptionRequest
{
    /// <summary>
    /// Gets the EventSub subscription type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the EventSub subscription version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the condition fields required by the subscription type.
    /// </summary>
    public IReadOnlyDictionary<string, string> Condition { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the EventSub WebSocket session identifier.
    /// </summary>
    public required string SessionId { get; init; }
}
