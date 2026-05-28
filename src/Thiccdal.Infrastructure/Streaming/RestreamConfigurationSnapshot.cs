namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Represents the live restream configuration snapshot exposed to the operator.
/// </summary>
public sealed record RestreamConfigurationSnapshot
{
    /// <summary>
    /// Gets the configured RTMP ingest URL.
    /// </summary>
    public string IngestUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the configured recording output path.
    /// </summary>
    public string RecordingOutputPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether restreaming should start with the host.
    /// </summary>
    public bool StartWithHost { get; init; }

    /// <summary>
    /// Gets the optional BRB slate media path reserved for ingest disconnect recovery.
    /// </summary>
    public string BrbSlatePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the configured relay destinations.
    /// </summary>
    public IReadOnlyList<RestreamDestinationSnapshot> Destinations { get; init; } = Array.Empty<RestreamDestinationSnapshot>();
}

/// <summary>
/// Represents a single relay destination in the live restream configuration snapshot.
/// </summary>
public sealed record RestreamDestinationSnapshot
{
    /// <summary>
    /// Gets the destination platform name.
    /// </summary>
    public string PlatformName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the destination is enabled for fanout.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the platform can currently participate in restream fanout.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the platform currently has a resolved RTMP destination.
    /// </summary>
    public bool IsRelayConfigured { get; init; }

    /// <summary>
    /// Gets the current platform connection state label.
    /// </summary>
    public string ConnectionState { get; init; } = string.Empty;

    /// <summary>
    /// Gets the relay readiness note shown beside the destination.
    /// </summary>
    public string RelayStatus { get; init; } = string.Empty;
}
