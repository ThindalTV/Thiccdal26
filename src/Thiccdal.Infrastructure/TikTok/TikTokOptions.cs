namespace Thiccdal.Infrastructure.TikTok;

/// <summary>
/// Configuration options for TikTok Live integration.
/// TikTok Live requires explicit API approval before any streaming operations are available.
/// </summary>
public sealed class TikTokOptions
{
    public const string SectionName = "TikTok";

    /// <summary>
    /// Gets or sets a value indicating whether TikTok Live integration is enabled.
    /// Note: This will remain false until TikTok approves the Open API access request.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the TikTok creator ID (required for streaming).
    /// </summary>
    public string CreatorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TikTok access token (bearer token for API calls).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RTMP server URL provided by TikTok Live Studio.
    /// Example: rtmp://live-push.tiktok.com:1935/live/
    /// </summary>
    public string RtmpServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream key provided by TikTok Live Studio.
    /// </summary>
    public string StreamKey { get; set; } = string.Empty;
}
