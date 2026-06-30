namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Identifies a single relay destination when pushing configuration to the remote RTMP server.
/// </summary>
/// <param name="PlatformName">The name of the streaming platform (e.g. Twitch, YouTube).</param>
/// <param name="DestinationUrl">The RTMP destination URL including the stream key.</param>
public sealed record RtmpRelayDestinationPush(
    string PlatformName,
    string DestinationUrl);
