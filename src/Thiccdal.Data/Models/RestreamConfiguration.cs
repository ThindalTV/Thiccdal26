namespace Thiccdal.Data.Models;

/// <summary>
/// Persists the operator-configured restream settings used across stream sessions.
/// </summary>
public sealed class RestreamConfiguration
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the RTMP ingest URL that OBS should publish to.</summary>
    public string IngestUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory where local recordings should be saved.</summary>
    public string RecordingOutputPath { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether restreaming should start automatically when the host boots.</summary>
    public bool StartWithHost { get; set; }

    /// <summary>Gets or sets the path to the BRB slate file used when ingest is not connected.</summary>
    public string BrbSlatePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the base URL of the remote RTMP server (e.g., http://rtmp-server:8080).</summary>
    public string RtmpServerUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the API key used to authenticate with the remote RTMP server.</summary>
    public string RtmpServerApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
