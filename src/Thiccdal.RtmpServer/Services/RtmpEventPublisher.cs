using Microsoft.AspNetCore.SignalR;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.RtmpServer.Hubs;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Publishes typed RTMP server events to all connected SignalR clients.
/// </summary>
public sealed class RtmpEventPublisher : IRtmpEventPublisher
{
    private readonly IHubContext<RtmpEventsHub> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtmpEventPublisher"/> class.
    /// </summary>
    public RtmpEventPublisher(IHubContext<RtmpEventsHub> hubContext)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        _hubContext = hubContext;
    }

    /// <summary>
    /// Publishes an ingest-connected event for the given stream path.
    /// </summary>
    public Task PublishIngestConnected(string streamPath, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "EventReceived",
            new RtmpServerEvent(RtmpServerEventType.IngestConnected, streamPath, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    /// <summary>
    /// Publishes an ingest-disconnected event for the given stream path.
    /// </summary>
    public Task PublishIngestDisconnected(string streamPath, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "EventReceived",
            new RtmpServerEvent(RtmpServerEventType.IngestDisconnected, streamPath, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    /// <summary>
    /// Publishes an ingest-error event with the given message.
    /// </summary>
    public Task PublishIngestError(string message, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "EventReceived",
            new RtmpServerEvent(RtmpServerEventType.IngestError, message, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    /// <summary>
    /// Publishes a recording-started event.
    /// </summary>
    public Task PublishRecordingStarted(CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "EventReceived",
            new RtmpServerEvent(RtmpServerEventType.RecordingStarted, "Recording started.", DateTimeOffset.UtcNow),
            cancellationToken);
    }

    /// <summary>
    /// Publishes a recording-ended event.
    /// </summary>
    public Task PublishRecordingEnded(CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "EventReceived",
            new RtmpServerEvent(RtmpServerEventType.RecordingEnded, "Recording ended.", DateTimeOffset.UtcNow),
            cancellationToken);
    }

    /// <summary>
    /// Publishes a relay-failed event for the given platform.
    /// </summary>
    public Task PublishRelayFailed(string platformName, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "EventReceived",
            new RtmpServerEvent(RtmpServerEventType.RelayFailed, $"Relay to {platformName} failed.", DateTimeOffset.UtcNow),
            cancellationToken);
    }
}
