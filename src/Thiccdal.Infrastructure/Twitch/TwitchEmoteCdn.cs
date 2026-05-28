namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Builds deterministic Twitch emote CDN URLs without performing HTTP metadata lookups.
/// </summary>
public static class TwitchEmoteCdn
{
    /// <summary>
    /// Gets the CDN URL for a Twitch emote.
    /// </summary>
    /// <param name="emoteId">The Twitch emote identifier.</param>
    /// <param name="animated">True to request the animated asset path; otherwise static.</param>
    /// <returns>The absolute emote CDN URL.</returns>
    public static string GetUrl(string emoteId, bool animated)
    {
        if (string.IsNullOrWhiteSpace(emoteId))
        {
            throw new ArgumentException("A Twitch emote id is required.", nameof(emoteId));
        }

        string format = animated ? "animated" : "default";
        return $"https://static-cdn.jtvnw.net/emoticons/v2/{emoteId}/{format}/dark/1.0";
    }
}
