namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Supplies the RTMP publish endpoint for a platform adapter when that adapter can participate in fanout.
/// </summary>
public interface IRtmpRelayDestinationProvider
{
    /// <summary>
    /// Gets the platform name associated with this relay destination.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Resolves the current RTMP publish destination for the platform.
    /// </summary>
    Task<RtmpRelayDestination?> GetRelayDestination(CancellationToken cancellationToken = default);
}
