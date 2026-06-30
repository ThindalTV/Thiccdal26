using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Publishes BRB slate relays to the currently armed fanout destinations.
/// </summary>
public sealed class BrbSlateInjector : IBrbSlateInjector
{
    private readonly IStreamingRelaySessionFactory _relaySessionFactory;
    private readonly IRtmpServerConfigurationHolder _holder;
    private readonly ILogger<BrbSlateInjector> _logger;
    private readonly Lock _stateLock = new();
    private Dictionary<string, IStreamingRelaySession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="BrbSlateInjector"/> class.
    /// </summary>
    public BrbSlateInjector(
        IStreamingRelaySessionFactory relaySessionFactory,
        IRtmpServerConfigurationHolder holder,
        ILogger<BrbSlateInjector> logger)
    {
        ArgumentNullException.ThrowIfNull(relaySessionFactory);
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(logger);

        _relaySessionFactory = relaySessionFactory;
        _holder = holder;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_stateLock)
            {
                return _sessions.Count > 0;
            }
        }
    }

    /// <inheritdoc />
    public async Task Start(IReadOnlyList<RtmpRelayDestination> destinations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinations);
        cancellationToken.ThrowIfCancellationRequested();

        await Stop(cancellationToken);

        if (destinations.Count == 0)
        {
            return;
        }

        string slatePath = _holder.GetCurrent().BrbSlatePath.Trim();
        if (string.IsNullOrWhiteSpace(slatePath) || !File.Exists(slatePath))
        {
            _logger.LogWarning(
                "BRB slate injection was requested for {DestinationCount} destination(s), but BrbSlatePath is not configured to an existing media file.",
                destinations.Count);
            return;
        }

        Dictionary<string, IStreamingRelaySession> sessions = new(StringComparer.OrdinalIgnoreCase);
        foreach (RtmpRelayDestination destination in destinations)
        {
            try
            {
                IStreamingRelaySession session = await _relaySessionFactory.StartBrbRelay(
                    destination.PlatformName,
                    slatePath,
                    destination.DestinationUrl,
                    cancellationToken);
                sessions[destination.PlatformName] = session;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to start BRB relay for {PlatformName}.", destination.PlatformName);
            }
        }

        lock (_stateLock)
        {
            _sessions = sessions;
        }

        if (sessions.Count > 0)
        {
            _logger.LogInformation("BRB slate injection started for {DestinationCount} destination(s).", sessions.Count);
        }
    }

    /// <inheritdoc />
    public async Task Stop(CancellationToken cancellationToken = default)
    {
        Dictionary<string, IStreamingRelaySession> sessions;
        lock (_stateLock)
        {
            sessions = _sessions;
            _sessions = new Dictionary<string, IStreamingRelaySession>(StringComparer.OrdinalIgnoreCase);
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
                _logger.LogWarning(ex, "Failed to stop BRB relay session for {PlatformName}.", session.PlatformName);
            }
        }

        if (sessions.Count > 0)
        {
            _logger.LogInformation("BRB slate injection stopped.");
        }
    }
}
