namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Represents a persisted local recording row.
/// </summary>
public sealed record StreamRecordingSnapshot
{
    /// <summary>
    /// Gets the recording row identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the optional operator live-session identifier.
    /// </summary>
    public Guid? SessionId { get; init; }

    /// <summary>
    /// Gets the platform label for this recording row.
    /// </summary>
    public string Platform { get; init; } = string.Empty;

    /// <summary>
    /// Gets the persisted recording file path.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets when recording started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets when recording ended, if it has ended.
    /// </summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>
    /// Gets the persisted error, if the recording failed or stopped uncleanly.
    /// </summary>
    public string Error { get; init; } = string.Empty;
}
