namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Client that communicates with the standalone remote RTMP server process via HTTP and SignalR.
/// </summary>
public interface IRtmpServerClient
{
    /// <summary>
    /// Gets a value indicating whether the SignalR event hub is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Raised when the remote RTMP server emits an event over the SignalR hub.
    /// </summary>
    event EventHandler<RtmpServerEvent> EventReceived;

    /// <summary>
    /// Connects to the RTMP server's SignalR event hub.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task Connect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the SignalR event hub.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task Disconnect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current runtime status of the RTMP server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<RtmpServerStatusResponse> GetStatus(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the full streaming configuration to the RTMP server.
    /// </summary>
    /// <param name="config">The configuration payload to push.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<RtmpServerStatusResponse> PushConfiguration(RtmpServerConfigurationPush config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Instructs the RTMP server to start accepting ingest and begin fanout.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<RtmpServerStatusResponse> Start(CancellationToken cancellationToken = default);

    /// <summary>
    /// Instructs the RTMP server to stop ingest and fanout.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<RtmpServerStatusResponse> Stop(CancellationToken cancellationToken = default);
}
