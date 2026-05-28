using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

/// <summary>
/// Tracks the current RTMP fanout lifecycle until platform relay contracts are formalized.
/// </summary>
public sealed class RtmpFanoutService : IRtmpFanoutService
{
    private readonly IReadOnlyList<IStreamTarget> _streamTargets;
    private readonly IReadOnlyDictionary<string, IPlatformConnection> _platformConnections;
    private readonly IReadOnlyDictionary<string, IRtmpRelayDestinationProvider> _relayProviders;
    private readonly IRestreamSettingsAccessor _settingsAccessor;
    private readonly IStreamingService _streamingService;
    private readonly IStreamingRelaySessionFactory _relaySessionFactory;
    private readonly IBrbSlateInjector _brbSlateInjector;
    private readonly ILogger<RtmpFanoutService> _logger;
    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private Dictionary<string, IStreamingRelaySession> _liveRelaySessions = new(StringComparer.OrdinalIgnoreCase);
    private bool _isRunning;

    public RtmpFanoutService(
        IEnumerable<IStreamTarget> streamTargets,
        IEnumerable<IRtmpRelayDestinationProvider> relayProviders,
        IRestreamSettingsAccessor settingsAccessor,
        IStreamingService streamingService,
        IStreamingRelaySessionFactory relaySessionFactory,
        IBrbSlateInjector brbSlateInjector,
        ILogger<RtmpFanoutService> logger)
    {
        ArgumentNullException.ThrowIfNull(streamTargets);
        ArgumentNullException.ThrowIfNull(relayProviders);
        ArgumentNullException.ThrowIfNull(settingsAccessor);
        ArgumentNullException.ThrowIfNull(streamingService);
        ArgumentNullException.ThrowIfNull(relaySessionFactory);
        ArgumentNullException.ThrowIfNull(brbSlateInjector);
        ArgumentNullException.ThrowIfNull(logger);

        _streamTargets = streamTargets.ToArray();
        _platformConnections = _streamTargets
            .OfType<IPlatformConnection>()
            .GroupBy(static target => target.PlatformName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        _relayProviders = relayProviders
            .GroupBy(static provider => provider.PlatformName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        _settingsAccessor = settingsAccessor;
        _streamingService = streamingService;
        _relaySessionFactory = relaySessionFactory;
        _brbSlateInjector = brbSlateInjector;
        _logger = logger;
        _streamingService.StateChanged += OnStreamingStateChanged;
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

    public async Task StartFanout(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<RtmpRelayDestination> activeDestinations = await GetActiveDestinations(cancellationToken);
        if (activeDestinations.Count == 0)
        {
            throw new InvalidOperationException("Restream fanout cannot start because no enabled relay destinations are configured.");
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
                "Marked RTMP fanout as started for {TargetCount} registered stream targets and {DestinationCount} resolved relay destinations.",
                _streamTargets.Count,
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
            IReadOnlyList<RtmpRelayDestination> activeDestinations = await GetActiveDestinations();

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

        Dictionary<string, IStreamingRelaySession> liveRelaySessions = new(StringComparer.OrdinalIgnoreCase);

        foreach (RtmpRelayDestination destination in destinations)
        {
            try
            {
                IStreamingRelaySession session = await _relaySessionFactory.StartLiveRelay(
                    destination.PlatformName,
                    _settingsAccessor.GetCurrent().IngestUrl,
                    destination.DestinationUrl,
                    cancellationToken);
                liveRelaySessions[destination.PlatformName] = session;
                _logger.LogInformation("Relay started to {PlatformName}.", destination.PlatformName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Relay to {PlatformName} failed; continuing with other targets.", destination.PlatformName);
            }
        }

        _liveRelaySessions = liveRelaySessions;
    }

    private async Task StopLiveRelays(CancellationToken cancellationToken = default)
    {
        Dictionary<string, IStreamingRelaySession> sessions = _liveRelaySessions;
        _liveRelaySessions = new Dictionary<string, IStreamingRelaySession>(StringComparer.OrdinalIgnoreCase);

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

    private async Task<IReadOnlyList<RtmpRelayDestination>> GetActiveDestinations(CancellationToken cancellationToken = default)
    {
        RestreamConfigurationSnapshot snapshot = _settingsAccessor.GetCurrent();
        HashSet<string>? explicitlyEnabledPlatforms = snapshot.Destinations.Count > 0
            ? snapshot.Destinations
                .Where(static destination => destination.IsEnabled)
                .Select(static destination => destination.PlatformName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        List<RtmpRelayDestination> destinations = new();

        foreach ((string platformName, IRtmpRelayDestinationProvider provider) in _relayProviders)
        {
            if (explicitlyEnabledPlatforms is not null && !explicitlyEnabledPlatforms.Contains(platformName))
            {
                continue;
            }

            if (_platformConnections.TryGetValue(platformName, out IPlatformConnection? platformConnection))
            {
                switch (platformConnection.State)
                {
                    case PlatformConnectionState.PendingApproval:
                    case PlatformConnectionState.Disabled:
                        _logger.LogDebug("Skipping {PlatformName} RTMP relay because the platform is {State}.", platformName, platformConnection.State);
                        continue;
                    case PlatformConnectionState.Error:
                        _logger.LogWarning("Skipping {PlatformName} RTMP relay because the platform is in an error state.", platformName);
                        continue;
                    case PlatformConnectionState.Connected:
                        break;
                    default:
                        _logger.LogInformation("Skipping {PlatformName} RTMP relay because the platform is not connected.", platformName);
                        continue;
                }
            }

            RtmpRelayDestination? destination = await provider.GetRelayDestination(cancellationToken);
            if (destination is not null)
            {
                destinations.Add(destination);
            }
        }

        return destinations;
    }
}
