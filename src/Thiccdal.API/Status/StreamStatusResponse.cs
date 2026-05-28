namespace Thiccdal.API.Status;

/// <summary>
/// Common state values used by the public status response.
/// </summary>
public static class StreamStatusStates
{
    /// <summary>
    /// Indicates that at least one configured platform is currently live.
    /// </summary>
    public const string Online = "Online";

    /// <summary>
    /// Indicates that no configured platforms are currently live.
    /// </summary>
    public const string Offline = "Offline";
}

/// <summary>
/// Represents the public stream status payload returned by <c>GET /status</c>.
/// </summary>
public sealed record StreamStatusResponse
{
    /// <summary>
    /// Gets the overall stream state.
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current live stream details when the stream is online.
    /// </summary>
    public StreamInfoDto? Stream { get; init; }

    /// <summary>
    /// Gets the per-platform status list.
    /// </summary>
    public IReadOnlyList<PlatformStatusDto> Platforms { get; init; } = [];
}

/// <summary>
/// Represents the current live stream details included in the public status payload.
/// </summary>
public sealed record StreamInfoDto
{
    /// <summary>
    /// Gets the current stream title when one is available.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current stream category when one is available.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current stream tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the timestamp when the current stream started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets the formatted uptime when it is available.
    /// </summary>
    public string Uptime { get; init; } = string.Empty;
}

/// <summary>
/// Represents the public status of a single destination platform.
/// </summary>
public sealed record PlatformStatusDto
{
    /// <summary>
    /// Gets the platform display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the platform state label.
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Gets the platform error message when a degraded state is reported.
    /// </summary>
    public string? Error { get; init; }
}
