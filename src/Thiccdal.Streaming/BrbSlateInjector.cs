using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

public sealed class BrbSlateInjector : IBrbSlateInjector
{
    private readonly IStreamingRelaySessionFactory _relaySessionFactory;
    private readonly StreamingOptions _options;
    private readonly ILogger<BrbSlateInjector> _logger;
    private readonly Lock _stateLock = new();
    private Dictionary<string, IStreamingRelaySession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public BrbSlateInjector(
        IStreamingRelaySessionFactory relaySessionFactory,
        IOptions<StreamingOptions> options,
        ILogger<BrbSlateInjector> logger)
    {
        ArgumentNullException.ThrowIfNull(relaySessionFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _relaySessionFactory = relaySessionFactory;
        _options = options.Value;
        _logger = logger;
    }

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

    public async Task Start(IReadOnlyList<RtmpRelayDestination> destinations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinations);
        cancellationToken.ThrowIfCancellationRequested();

        await Stop(cancellationToken);

        if (destinations.Count == 0)
        {
            return;
        }

        string slatePath = _options.BrbSlatePath.Trim();
        if (string.IsNullOrWhiteSpace(slatePath) || !File.Exists(slatePath))
        {
            _logger.LogWarning(
                "BRB slate injection was requested for {DestinationCount} destination(s), but Streaming:BrbSlatePath is not configured to an existing media file.",
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
