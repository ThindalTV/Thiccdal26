namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// The full configuration payload pushed to the remote RTMP server before going live.
/// </summary>
/// <param name="IngestUrl">The RTMP ingest URL that OBS should publish to.</param>
/// <param name="RecordingOutputPath">The directory on the RTMP server host where recordings should be saved.</param>
/// <param name="BrbSlatePath">The path to the BRB slate file used when ingest is not connected.</param>
/// <param name="Destinations">The list of relay destinations to fan out to.</param>
public sealed record RtmpServerConfigurationPush(
    string IngestUrl,
    string RecordingOutputPath,
    string BrbSlatePath,
    IReadOnlyList<RtmpRelayDestinationPush> Destinations);
