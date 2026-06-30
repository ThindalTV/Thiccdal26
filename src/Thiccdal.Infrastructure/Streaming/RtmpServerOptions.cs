namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Configuration options for connecting to the remote RTMP server process.
/// </summary>
public sealed class RtmpServerOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.
    /// </summary>
    public const string SectionName = "RtmpServer";

    /// <summary>
    /// Gets or sets the base URL of the remote RTMP server (e.g., http://rtmp-server:8080).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key used to authenticate requests to the remote RTMP server.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the FFmpeg executable path on the RTMP server host.
    /// </summary>
    public string FfmpegExecutablePath { get; set; } = "ffmpeg";
}
