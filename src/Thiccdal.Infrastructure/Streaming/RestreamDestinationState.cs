namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Operator-facing restream state for a single platform destination.
/// </summary>
public sealed record RestreamDestinationState
{
    /// <summary>
    /// Gets the platform display name.
    /// </summary>
    public required string PlatformName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the underlying platform integration is connected.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Gets a value indicating whether the platform is selected for fanout.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the adapter exposes an RTMP relay destination.
    /// </summary>
    public bool SupportsRelay { get; init; }

    /// <summary>
    /// Gets a value indicating whether the adapter currently has a usable relay URL.
    /// </summary>
    public bool IsRelayConfigured { get; init; }

    /// <summary>
    /// Gets a note describing relay readiness for the destination.
    /// </summary>
    public string RelayStatus { get; init; } = string.Empty;
}
