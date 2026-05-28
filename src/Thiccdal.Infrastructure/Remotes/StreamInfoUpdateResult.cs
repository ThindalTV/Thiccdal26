namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents a platform-specific outcome for a stream metadata update attempt.
/// </summary>
public sealed record StreamInfoUpdateResult
{
    /// <summary>
    /// Gets the platform display name.
    /// </summary>
    public string PlatformName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the update outcome.
    /// </summary>
    public StreamInfoUpdateStatus Status { get; init; }

    /// <summary>
    /// Gets the human-readable outcome message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
