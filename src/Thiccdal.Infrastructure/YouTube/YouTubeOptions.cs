namespace Thiccdal.Infrastructure.YouTube;

public class YouTubeOptions
{
    public const string SectionName = "YouTube";
    public const string DefaultOAuthBaseAddress = "https://accounts.google.com/o/oauth2/v2/";
    public const string DefaultApiBaseAddress = "https://www.googleapis.com/youtube/v3/";

    /// <summary>
    /// Gets or sets the default YouTube channel ID the bot should monitor when no UI override has been saved.
    /// </summary>
    public string DefaultChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Google Cloud OAuth client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Google Cloud OAuth client secret.
    /// Updating broadcast title or description requires OAuth with <c>youtube.force-ssl</c>.
    /// Store this outside source control, and keep refresh tokens in secure storage such as user secrets or environment variables.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OAuth redirect URI registered in Google Cloud Console for the write-capable YouTube OAuth flow.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base address for Google OAuth endpoints.
    /// </summary>
    public string OAuthBaseAddress { get; set; } = DefaultOAuthBaseAddress;

    /// <summary>
    /// Gets or sets the base address for YouTube Data API v3 endpoints.
    /// </summary>
    public string ApiBaseAddress { get; set; } = DefaultApiBaseAddress;

    /// <summary>
    /// Gets or sets the live chat polling interval in seconds.
    /// YouTube rate limits apply; default is 5 seconds per YouTube documentation.
    /// </summary>
    public int LiveChatPollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the broadcast info refresh interval in seconds.
    /// </summary>
    public int BroadcastInfoRefreshSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the OAuth scopes requested for YouTube access.
    /// Include <c>youtube.force-ssl</c> when operator features need to update broadcast title or description.
    /// </summary>
    public List<string> Scopes { get; set; } = new()
    {
        "https://www.googleapis.com/auth/youtube.readonly",
        "https://www.googleapis.com/auth/youtube.force-ssl"
    };

    /// <summary>
    /// Gets or sets the fallback live chat polling interval in milliseconds used when the API omits a hint.
    /// </summary>
    public int PollFallbackIntervalMillis
    {
        get => LiveChatPollingIntervalSeconds * 1000;
        set => LiveChatPollingIntervalSeconds = value <= 0 ? 5 : Math.Max(1, (int)Math.Ceiling(value / 1000d));
    }

    /// <summary>
    /// Legacy alias preserved for compatibility.
    /// Prefer <see cref="DefaultChannelId"/>.
    /// </summary>
    public string ChannelId
    {
        get => DefaultChannelId;
        set => DefaultChannelId = value;
    }
}
