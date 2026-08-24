namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Configures the connection to the obs-websocket server exposed by OBS Studio on the stream PC.
/// </summary>
public sealed class ObsOptions
{
    public const string SectionName = "Obs";

    /// <summary>
    /// Gets or sets a value indicating whether Thiccdal connects to OBS at all.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the host running obs-websocket. OBS runs on the stream PC alongside Thiccdal.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the obs-websocket port.
    /// </summary>
    public int Port { get; set; } = 4455;

    /// <summary>
    /// Gets or sets the obs-websocket server password. Empty when authentication is disabled in OBS.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delay before the first reconnect attempt after the connection drops.
    /// </summary>
    public int InitialReconnectDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets the ceiling the reconnect backoff grows to.
    /// </summary>
    public int MaxReconnectDelaySeconds { get; set; } = 60;
}
