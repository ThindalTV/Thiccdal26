namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Wraps YouTube Data API v3 calls.
/// </summary>
public interface IYouTubeApiClient
{
    /// <summary>Fetches metadata for the currently active live broadcast on the authenticated channel.</summary>
    Task<YouTubeBroadcastInfo?> GetActiveBroadcast(CancellationToken cancellationToken = default);

    /// <summary>Polls live chat messages from the specified chat ID, starting after the given page token.</summary>
    Task<YouTubeLiveChatPollResult> PollLiveChat(string liveChatId, string? pageToken, CancellationToken cancellationToken = default);

    /// <summary>Sends a text message to the specified live chat.</summary>
    Task SendLiveChatMessage(string liveChatId, string message, CancellationToken cancellationToken = default);

    /// <summary>Updates the broadcast title and description.</summary>
    Task UpdateBroadcastInfo(string broadcastId, string title, string description, CancellationToken cancellationToken = default);
}
