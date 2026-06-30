namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Publishes typed RTMP server events to connected SignalR clients.
/// </summary>
public interface IRtmpEventPublisher
{
    /// <summary>Publishes an ingest-connected event for the given stream path.</summary>
    Task PublishIngestConnected(string streamPath, CancellationToken cancellationToken = default);

    /// <summary>Publishes an ingest-disconnected event for the given stream path.</summary>
    Task PublishIngestDisconnected(string streamPath, CancellationToken cancellationToken = default);

    /// <summary>Publishes an ingest-error event with the given message.</summary>
    Task PublishIngestError(string message, CancellationToken cancellationToken = default);

    /// <summary>Publishes a recording-started event.</summary>
    Task PublishRecordingStarted(CancellationToken cancellationToken = default);

    /// <summary>Publishes a recording-ended event.</summary>
    Task PublishRecordingEnded(CancellationToken cancellationToken = default);

    /// <summary>Publishes a relay-failed event for the given platform.</summary>
    Task PublishRelayFailed(string platformName, CancellationToken cancellationToken = default);
}
