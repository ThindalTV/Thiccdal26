using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Thread-safe in-memory store for the runtime configuration pushed from the bot.
/// </summary>
public sealed class RtmpServerConfigurationHolder : IRtmpServerConfigurationHolder
{
    private readonly Lock _lock = new();
    private RtmpServerConfigurationPush _current = new RtmpServerConfigurationPush(
        IngestUrl: string.Empty,
        RecordingOutputPath: string.Empty,
        BrbSlatePath: string.Empty,
        Destinations: Array.Empty<RtmpRelayDestinationPush>());

    /// <inheritdoc />
    public RtmpServerConfigurationPush GetCurrent()
    {
        lock (_lock)
        {
            return _current;
        }
    }

    /// <inheritdoc />
    public void Apply(RtmpServerConfigurationPush configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        lock (_lock)
        {
            _current = configuration;
        }
    }
}
