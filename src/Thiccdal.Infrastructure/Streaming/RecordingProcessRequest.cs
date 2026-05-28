namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Describes the FFmpeg recording process that should be launched.
/// </summary>
public sealed record RecordingProcessRequest
{
    /// <summary>
    /// Gets the executable path for FFmpeg.
    /// </summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Gets the RTMP ingest URL to read from.
    /// </summary>
    public required string IngestUrl { get; init; }

    /// <summary>
    /// Gets the recording output file path.
    /// </summary>
    public required string OutputPath { get; init; }
}
