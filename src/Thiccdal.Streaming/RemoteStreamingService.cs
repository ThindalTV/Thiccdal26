using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

/// <summary>
/// Implements <see cref="IStreamingService"/> by delegating to the remote RTMP server
/// and tracking local state from events received over the SignalR hub.
/// </summary>
public sealed class RemoteStreamingService : IStreamingService, IHostedService
{
    private readonly IRtmpServerClient _client;
    private readonly ILogger<RemoteStreamingService> _logger;
    private readonly Lock _stateLock = new();
    private StreamingState _state = StreamingState.Idle;
    private bool _isRunning;

    /// <summary>
    /// Initializes a new instance of <see cref="RemoteStreamingService"/>.
    /// </summary>
    public RemoteStreamingService(
        IRtmpServerClient client,
        ILogger<RemoteStreamingService> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
        _client.EventReceived += OnEventReceived;
    }

    /// <inheritdoc/>
    public bool IsRunning
    {
        get
        {
            lock (_stateLock)
            {
                return _isRunning;
            }
        }
    }

    /// <inheritdoc/>
    public StreamingState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<StreamingState>? StateChanged;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _client.Connect(cancellationToken);
        _logger.LogInformation("Remote streaming service connected to RTMP server.");
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.Disconnect(cancellationToken);
        _logger.LogInformation("Remote streaming service disconnected from RTMP server.");
    }

    /// <inheritdoc/>
    public async Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null)
    {
        _ = sessionId;

        RtmpServerStatusResponse result = await _client.Start(cancellationToken);
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            _logger.LogError("RTMP server rejected start: {ErrorMessage}", result.ErrorMessage);
        }

        lock (_stateLock)
        {
            _isRunning = true;
        }

        SetState(StreamingState.WaitingForIngest);
        _logger.LogInformation("Streaming start issued to remote RTMP server.");
    }

    /// <inheritdoc/>
    public async Task Stop(CancellationToken cancellationToken = default)
    {
        RtmpServerStatusResponse result = await _client.Stop(cancellationToken);
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            _logger.LogWarning("RTMP server returned error on stop: {ErrorMessage}", result.ErrorMessage);
        }

        lock (_stateLock)
        {
            _isRunning = false;
        }

        SetState(StreamingState.Idle);
        _logger.LogInformation("Streaming stop issued to remote RTMP server.");
    }

    private void OnEventReceived(object? sender, RtmpServerEvent rtmpEvent)
    {
        _ = sender;

        StreamingState? nextState = rtmpEvent.EventType switch
        {
            RtmpServerEventType.IngestConnected => StreamingState.Live,
            RtmpServerEventType.IngestDisconnected => StreamingState.BrbSlate,
            RtmpServerEventType.IngestError => StreamingState.Error,
            _ => null
        };

        if (nextState.HasValue)
        {
            SetState(nextState.Value);
            _logger.LogInformation(
                "Remote streaming state updated to {State} from RTMP server event {EventType}: {Message}",
                nextState.Value,
                rtmpEvent.EventType,
                rtmpEvent.Message);
        }
    }

    private void SetState(StreamingState state)
    {
        bool changed;

        lock (_stateLock)
        {
            changed = _state != state;
            _state = state;
        }

        if (changed)
        {
            StateChanged?.Invoke(this, state);
        }
    }
}
