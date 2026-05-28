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
    /// Gets or sets the ingest URL that OBS should publish to.
    /// </summary>
    public string IngestUrl { get; set; } = "rtmp://localhost:1935/live";

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
