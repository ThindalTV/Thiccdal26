namespace Thiccdal.Infrastructure.Instagram;

/// <summary>
/// Configuration options for Instagram Live integration.
/// Instagram Live requires explicit API approval from Meta before any live streaming operations are available.
/// </summary>
public sealed class InstagramOptions
{
    public const string SectionName = "Instagram";

    /// <summary>
    /// Gets or sets a value indicating whether Instagram Live integration is enabled.
    /// Note: This will remain false until Meta approves the Instagram Live API access request.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Meta app ID.
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Meta app secret.
    /// </summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Instagram user ID or page ID for the broadcaster's account.
    /// </summary>
    public string BroadcasterId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access token (Instagram User Token or Page Token).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RTMP stream URL from Instagram Live Studio (provided at broadcast creation time).
    /// Example: rtmps://live-api-s.facebook.com:443/rtmp/
    /// </summary>
    public string RtmpServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream key provided by Instagram Live Studio.
    /// </summary>
    public string StreamKey { get; set; } = string.Empty;
}
