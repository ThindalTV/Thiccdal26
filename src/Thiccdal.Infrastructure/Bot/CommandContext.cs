namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Carries the normalized command invocation details passed to chatbot handlers and token interpolation.
/// </summary>
public sealed record CommandContext
{
    /// <summary>
    /// Gets the normalized trigger that matched the command.
    /// </summary>
    public required string Trigger { get; init; }

    /// <summary>
    /// Gets the whitespace-delimited arguments that followed the trigger.
    /// </summary>
    public string[] Args { get; init; } = [];

    /// <summary>
    /// Gets the requesting chatter's display name.
    /// </summary>
    public required string UserDisplayName { get; init; }

    /// <summary>
    /// Gets the normalized platform display name for the originating chat message.
    /// </summary>
    public required string Platform { get; init; }

    /// <summary>
    /// Gets the typed platform source for platform-scoped reply routing.
    /// </summary>
    public required Models.PlatformEventSource SourcePlatform { get; init; }

    /// <summary>
    /// Gets the platform-specific channel identifier for platform-scoped replies.
    /// </summary>
    public string? ChannelId { get; init; }

    /// <summary>
    /// Gets the number of times the command has been triggered during the current app session.
    /// </summary>
    public int UseCount { get; init; }

    /// <summary>
    /// Gets the active stream start time when the operator is live.
    /// </summary>
    public DateTimeOffset? StreamStartedAt { get; init; }
}
