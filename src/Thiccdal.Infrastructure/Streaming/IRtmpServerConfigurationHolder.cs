namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Holds the mutable runtime configuration pushed from the bot to the RTMP server.
/// </summary>
public interface IRtmpServerConfigurationHolder
{
    /// <summary>
    /// Returns the current configuration snapshot.
    /// </summary>
    RtmpServerConfigurationPush GetCurrent();

    /// <summary>
    /// Applies a new configuration snapshot, replacing the current one.
    /// </summary>
    /// <param name="configuration">The configuration to apply.</param>
    void Apply(RtmpServerConfigurationPush configuration);
}
