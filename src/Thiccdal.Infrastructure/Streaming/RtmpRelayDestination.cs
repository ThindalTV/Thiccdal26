namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Represents a resolved RTMP fanout destination.
/// </summary>
public sealed record RtmpRelayDestination
{
    /// <summary>
    /// Gets the destination platform name.
    /// </summary>
    public required string PlatformName { get; init; }

    /// <summary>
    /// Gets the full RTMP endpoint URL to publish to.
    /// </summary>
    public required string DestinationUrl { get; init; }
}
