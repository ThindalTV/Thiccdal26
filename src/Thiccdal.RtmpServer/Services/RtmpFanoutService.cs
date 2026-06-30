using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Coordinates RTMP fanout for the standalone RTMP server process.
/// Destinations are sourced from the configuration holder rather than platform adapters.
/// </summary>
public sealed class RtmpFanoutService : IRtmpFanoutService
{
    private readonly IRtmpServerConfigurationHolder _holder;
    private readonly IStreamingService _streamingService;
    private readonly IStreamingRelaySessionFactory _relaySessionFactory;
    private readonly IBrbSlateInjector _brbSlateInjector;
    private readonly IRtmpEventPublisher _eventPublisher;
    private readonly ILogger<RtmpFanoutService> _logger;
    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private Dictionary<string, IStreamingRelaySession> _liveRelaySessions = new(StringComparer.OrdinalIgnoreCase);
    private bool _isRunning;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtmpFanoutService"/> class.
    /// </summary>
    public RtmpFanoutService(
        IRtmpServerConfigurationHolder holder,
        IStreamingService streamingService,
        IStreamingRelaySessionFactory relaySessionFactory,
        IBrbSlateInjector brbSlateInjector,
        IRtmpEventPublisher eventPublisher,
        ILogger<RtmpFanoutService> logger)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(streamingService);
        ArgumentNullException.ThrowIfNull(relaySessionFactory);
        ArgumentNullException.ThrowIfNull(brbSlateInjector);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(logger);

        _holder = holder;
        _streamingService = streamingService;
        _relaySessionFactory = relaySessionFactory;
        _brbSlateInjector = brbSlateInjector;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _streamingService.StateChanged += OnStreamingStateChanged;
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Gets the number of currently active relay sessions.
    /// </summary>
    public int ActiveRelayCount
    {
        get
        {
            lock (_stateLock)
            {
                return _liveRelaySessions.Count;
            }
        }
    }

    /// <inheritdoc />
    public async Task StartFanout(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<RtmpRelayDestination> activeDestinations = GetActiveDestinations();
        if (activeDestinations.Count == 0)
        {
            throw new InvalidOperationException("Restream fanout cannot start because no relay destinations are configured.");
        }

        bool started;

        lock (_stateLock)
        {
            started = !_isRunning;
            _isRunning = true;
        }

        if (started)
        {
            _logger.LogInformation(
                "Marked RTMP fanout as started for {DestinationCount} resolved relay destinations.",
                activeDestinations.Count);
        }

        if (_streamingService.State == StreamingState.Live)
        {
            await EnsureLiveRelays(activeDestinations, cancellationToken);
        }
        else if (_streamingService.State == StreamingState.BrbSlate)
        {
            await _brbSlateInjector.Start(activeDestinations, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task StopFanout(CancellationToken cancellationToken = default)
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
            await _transitionLock.WaitAsync(cancellationToken);
            try
            {
                await _brbSlateInjector.Stop(cancellationToken);
                await StopLiveRelays(cancellationToken);
            }
            finally
            {
                _transitionLock.Release();
            }

            _logger.LogInformation("Marked RTMP fanout as stopped.");
        }
    }

    private void OnStreamingStateChanged(object? sender, StreamingState state)
    {
        _ = sender;
        _ = HandleStreamingStateChange(state);
    }

    private async Task HandleStreamingStateChange(StreamingState state)
    {
        bool shouldRun;
        lock (_stateLock)
        {
            shouldRun = _isRunning;
        }

        if (!shouldRun)
        {
            return;
        }

        await _transitionLock.WaitAsync();
        try
        {
            IReadOnlyList<RtmpRelayDestination> activeDestinations = GetActiveDestinations();

            switch (state)
            {
                case StreamingState.Live:
                    await _brbSlateInjector.Stop();
                    await EnsureLiveRelays(activeDestinations);
                    break;
                case StreamingState.BrbSlate:
                    await StopLiveRelays();
                    await _brbSlateInjector.Start(activeDestinations);
                    break;
                case StreamingState.Error:
                case StreamingState.Idle:
                    await _brbSlateInjector.Stop();
                    await StopLiveRelays();
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to apply streaming state {State} to RTMP fanout.", state);
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    private async Task EnsureLiveRelays(IReadOnlyList<RtmpRelayDestination> destinations, CancellationToken cancellationToken = default)
    {
        await StopLiveRelays(cancellationToken);

        string ingestUrl = _holder.GetCurrent().IngestUrl;
        Dictionary<string, IStreamingRelaySession> liveRelaySessions = new(StringComparer.OrdinalIgnoreCase);

        foreach (RtmpRelayDestination destination in destinations)
        {
            try
            {
                IStreamingRelaySession session = await _relaySessionFactory.StartLiveRelay(
                    destination.PlatformName,
                    ingestUrl,
                    destination.DestinationUrl,
                    cancellationToken);
                liveRelaySessions[destination.PlatformName] = session;
                _logger.LogInformation("Relay started to {PlatformName}.", destination.PlatformName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Relay to {PlatformName} failed; continuing with other targets.", destination.PlatformName);
                _ = _eventPublisher.PublishRelayFailed(destination.PlatformName);
            }
        }

        lock (_stateLock)
        {
            _liveRelaySessions = liveRelaySessions;
        }
    }

    private async Task StopLiveRelays(CancellationToken cancellationToken = default)
    {
        Dictionary<string, IStreamingRelaySession> sessions;

        lock (_stateLock)
        {
            sessions = _liveRelaySessions;
            _liveRelaySessions = new Dictionary<string, IStreamingRelaySession>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (IStreamingRelaySession session in sessions.Values)
        {
            try
            {
                await session.Stop(cancellationToken);
                await session.DisposeAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to stop relay session for {PlatformName}.", session.PlatformName);
            }
        }
    }

    private IReadOnlyList<RtmpRelayDestination> GetActiveDestinations()
    {
        IReadOnlyList<RtmpRelayDestinationPush> pushDestinations = _holder.GetCurrent().Destinations;
        List<RtmpRelayDestination> destinations = new(pushDestinations.Count);

        foreach (RtmpRelayDestinationPush push in pushDestinations)
        {
            destinations.Add(new RtmpRelayDestination
            {
                PlatformName = push.PlatformName,
                DestinationUrl = push.DestinationUrl
            });
        }

        return destinations;
    }
}
