using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

/// <summary>
/// Coordinates ingest lifecycle with local disk recording.
/// </summary>
public sealed class StreamingService : IStreamingService
{
    private readonly IOptions<StreamingOptions> _options;
    private readonly IRtmpIngestListener _ingestListener;
    private readonly IDiskRecorder _diskRecorder;
    private readonly ILogger<StreamingService> _logger;
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private StreamingState _state = StreamingState.Idle;
    private Guid? _sessionId;

    public StreamingService(
        IOptions<StreamingOptions> options,
        IRtmpIngestListener ingestListener,
        IDiskRecorder diskRecorder,
        ILogger<StreamingService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ingestListener);
        ArgumentNullException.ThrowIfNull(diskRecorder);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _ingestListener = ingestListener;
        _diskRecorder = diskRecorder;
        _logger = logger;
        _ingestListener.StateChanged += OnIngestStateChanged;
    }

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

    public event EventHandler<StreamingState>? StateChanged;

    public async Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.Value.IngestUrl))
        {
            throw new InvalidOperationException("Streaming:IngestUrl must be configured before going live.");
        }

        bool started;

        lock (_stateLock)
        {
            started = !_isRunning;
            _isRunning = true;
            _sessionId = sessionId;
        }

        if (started)
        {
            try
            {
                await _ingestListener.Start(cancellationToken);
                SetState(StreamingState.WaitingForIngest);
                _logger.LogInformation(
                    "Streaming ingest listener is armed for {IngestUrl} and waiting for OBS.",
                    _options.Value.IngestUrl);
            }
            catch
            {
                lock (_stateLock)
                {
                    _isRunning = false;
                    _sessionId = null;
                }

                SetState(StreamingState.Error);
                throw;
            }
        }
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool stopped;

        lock (_stateLock)
        {
            stopped = _isRunning;
            _isRunning = false;
        }

        if (stopped)
        {
            await _diskRecorder.Stop(cancellationToken);
            await _ingestListener.Stop(cancellationToken);

            lock (_stateLock)
            {
                _sessionId = null;
            }

            SetState(StreamingState.Idle);
            _logger.LogInformation("Marked streaming ingest as stopped.");
        }
    }

    private void OnIngestStateChanged(object? sender, RtmpIngestStateChanged stateChanged)
    {
        _ = sender;
        _ = ApplyIngestState(stateChanged);
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

    private async Task ApplyIngestState(RtmpIngestStateChanged stateChanged)
    {
        bool shouldApply;
        Guid? sessionId;

        lock (_stateLock)
        {
            shouldApply = _isRunning;
            sessionId = _sessionId;
        }

        if (!shouldApply)
        {
            return;
        }

        if (stateChanged.State == StreamingState.Live)
        {
            await _diskRecorder.Start(sessionId: sessionId);
        }
        else if (stateChanged.State is StreamingState.BrbSlate or StreamingState.Error)
        {
            await _diskRecorder.Stop();
        }

        SetState(stateChanged.State);
        _logger.LogInformation(
            "Streaming ingest transitioned to {State}: {Message}",
            stateChanged.State,
            stateChanged.Message);
    }
}
