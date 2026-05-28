namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Represents metadata about a YouTube live broadcast.
/// </summary>
public record YouTubeBroadcastInfo
{
    /// <summary>Gets the live broadcast ID.</summary>
    public string BroadcastId { get; init; } = string.Empty;

    /// <summary>Gets the associated live chat ID for this broadcast.</summary>
    public string LiveChatId { get; init; } = string.Empty;

    /// <summary>Gets the broadcast title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the broadcast description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the broadcast category when available.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Gets the broadcast tags when available.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets a value indicating whether the broadcast is currently live.</summary>
    public bool IsLive { get; init; }

    /// <summary>Gets when the live broadcast started when available.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Gets the concurrent viewer count when available.</summary>
    public int? ConcurrentViewers { get; init; }
}
