namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Hosts the RTMP ingest listener that receives the operator's OBS publish.
/// </summary>
public interface IRtmpIngestListener
{
    /// <summary>
    /// Gets a value indicating whether the ingest listener is currently accepting connections.
    /// </summary>
    bool IsListening { get; }

    /// <summary>
    /// Raised when the ingest lifecycle changes.
    /// </summary>
    event EventHandler<RtmpIngestStateChanged>? StateChanged;

    /// <summary>
    /// Starts the ingest listener.
    /// </summary>
    Task Start(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the ingest listener.
    /// </summary>
    Task Stop(CancellationToken cancellationToken = default);
}
