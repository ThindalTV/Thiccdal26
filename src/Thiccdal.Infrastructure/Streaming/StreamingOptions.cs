namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Supplies operator-facing streaming configuration used by the restreamer foundation.
/// </summary>
public sealed class StreamingOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Streaming";

    /// <summary>
    /// Gets or sets the deployment mode for the RTMP server.
    /// </summary>
    public StreamingDeploymentMode DeploymentMode { get; set; } = StreamingDeploymentMode.Embedded;

    /// <summary>
    /// Gets or sets the ingest URL that OBS should publish to.
    /// For embedded mode, this is the local listener address.
    /// For external mode, this is the address of the external RTMP server.
    /// </summary>
    public string IngestUrl { get; set; } = "rtmp://localhost:1935/live";

    /// <summary>
    /// Gets or sets the host of the external RTMP server (used when DeploymentMode is External).
    /// </summary>
    public string ExternalRtmpHost { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API port of the external RTMP server for control plane communication.
    /// </summary>
    public int ExternalRtmpApiPort { get; set; } = 5100;

    /// <summary>
    /// Gets or sets the API key for authenticating with the external RTMP server.
    /// </summary>
    public string ExternalRtmpApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the folder where local recordings should be written.
    /// </summary>
    public string RecordingOutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the FFmpeg executable path used for local recording.
    /// </summary>
    public string FfmpegExecutablePath { get; set; } = "ffmpeg";

    /// <summary>
    /// Gets or sets a value indicating whether restreaming should start automatically when the host boots.
    /// </summary>
    public bool StartWithHost { get; set; }

    /// <summary>
    /// Gets or sets the optional file path for a BRB slate that will be used once ingest recovery is implemented.
    /// </summary>
    public string BrbSlatePath { get; set; } = string.Empty;

}

