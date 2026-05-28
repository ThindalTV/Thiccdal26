namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Operator request for updating the shared restream configuration.
/// </summary>
public sealed record RestreamConfigurationUpdateRequest
{
    /// <summary>
    /// Gets the RTMP ingest URL the operator wants OBS to target.
    /// </summary>
    public required string IngestUrl { get; init; }

    /// <summary>
    /// Gets the output folder for local stream recordings.
    /// </summary>
    public string RecordingOutputPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether restreaming should start automatically when the host boots.
    /// </summary>
    public bool StartWithHost { get; init; }

    /// <summary>
    /// Gets the optional BRB slate media path reserved for ingest disconnect recovery.
    /// </summary>
    public string BrbSlatePath { get; init; } = string.Empty;
}
