namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Result of a YouTube live chat polling request.
/// </summary>
public record YouTubeLiveChatPollResult
{
    /// <summary>Gets the next page token to pass in subsequent polls.</summary>
    public string NextPageToken { get; init; } = string.Empty;

    /// <summary>Gets the recommended polling interval in milliseconds.</summary>
    public int PollingIntervalMillis { get; init; }

    /// <summary>Gets the raw JSON response for event mapping.</summary>
    public string RawJson { get; init; } = string.Empty;
}
