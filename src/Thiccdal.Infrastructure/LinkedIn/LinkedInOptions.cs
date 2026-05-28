namespace Thiccdal.Infrastructure.LinkedIn;

/// <summary>
/// Configuration options for LinkedIn Live integration.
/// LinkedIn requires explicit API approval before any live streaming operations are available.
/// </summary>
public sealed class LinkedInOptions
{
    public const string SectionName = "LinkedIn";

    /// <summary>
    /// Gets or sets a value indicating whether LinkedIn integration is enabled.
    /// Note: This will remain false until LinkedIn approves the Live API access request.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the LinkedIn organization ID (required for streaming).
    /// </summary>
    public string OrganizationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the LinkedIn access token (bearer token for API calls).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RTMP ingest server URL provided by LinkedIn Live Studio.
    /// Example: rtmps://live-api.linkedin.com:443/live/
    /// </summary>
    public string RtmpIngestUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream key provided by LinkedIn Live Studio.
    /// </summary>
    public string StreamKey { get; set; } = string.Empty;
}
