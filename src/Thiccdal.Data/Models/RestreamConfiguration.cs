namespace Thiccdal.Data.Models;

public sealed class RestreamConfiguration
{
    public int Id { get; set; }

    public string IngestUrl { get; set; } = string.Empty;

    public string RecordingOutputPath { get; set; } = string.Empty;

    public bool StartWithHost { get; set; }

    public string BrbSlatePath { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
