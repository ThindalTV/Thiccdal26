namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Describes what Thiccdal currently knows about OBS Studio on the stream PC.
/// </summary>
public sealed record ObsState
{
    /// <summary>
    /// Gets a value indicating whether the OBS integration is switched on in configuration.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether an authenticated obs-websocket session is open.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Gets a value indicating whether OBS reports an active stream output.
    /// </summary>
    public bool IsStreaming { get; init; }

    /// <summary>
    /// Gets the reason the last connection attempt failed, or <see langword="null"/> when connected.
    /// </summary>
    public string? LastError { get; init; }
}
