using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Data;

public sealed class RestreamRuntimeService : IRestreamRuntimeService, IRestreamSettingsAccessor
{
    private const string LocalRecordingPlatform = "Local";
    private const string DependencyNote = "RTMP ingest, destination selection, BRB orchestration, and disk-recording persistence are live here. Platform fanout is only available where an adapter exposes a concrete RTMP relay destination.";

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IReadOnlyList<IIntegrationConnectionMonitor> _integrationMonitors;
    private readonly IReadOnlyDictionary<string, IRtmpRelayDestinationProvider> _relayProviders;
    private readonly IStreamingService _streamingService;
    private readonly IRtmpFanoutService _fanoutService;
    private readonly IStreamRecordingService _streamRecordingService;
    private readonly IRtmpServerClient _rtmpServerClient;
    private readonly RtmpServerOptions _defaultRtmpServerOptions;
    private readonly StreamingOptions _defaultStreamingOptions;
    private readonly ILogger<RestreamRuntimeService> _logger;
    private readonly Lock _configurationLock = new();
    private RestreamConfigurationSnapshot _currentConfiguration;

    public RestreamRuntimeService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IEnumerable<IIntegrationConnectionMonitor> integrationMonitors,
        IEnumerable<IRtmpRelayDestinationProvider> relayProviders,
        IStreamingService streamingService,
        IRtmpFanoutService fanoutService,
        IStreamRecordingService streamRecordingService,
        IRtmpServerClient rtmpServerClient,
        IOptions<StreamingOptions> defaultStreamingOptions,
        IOptions<RtmpServerOptions> rtmpServerOptions,
        ILogger<RestreamRuntimeService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(integrationMonitors);
        ArgumentNullException.ThrowIfNull(relayProviders);
        ArgumentNullException.ThrowIfNull(streamingService);
        ArgumentNullException.ThrowIfNull(fanoutService);
        ArgumentNullException.ThrowIfNull(streamRecordingService);
        ArgumentNullException.ThrowIfNull(rtmpServerClient);
        ArgumentNullException.ThrowIfNull(defaultStreamingOptions);
        ArgumentNullException.ThrowIfNull(rtmpServerOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _integrationMonitors = integrationMonitors
            .OrderBy(static monitor => monitor.PlatformName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _relayProviders = relayProviders
            .GroupBy(static provider => provider.PlatformName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        _streamingService = streamingService;
        _fanoutService = fanoutService;
        _streamRecordingService = streamRecordingService;
        _rtmpServerClient = rtmpServerClient;
        _defaultRtmpServerOptions = rtmpServerOptions.Value;
        _defaultStreamingOptions = defaultStreamingOptions.Value;
        _logger = logger;
        _currentConfiguration = new RestreamConfigurationSnapshot
        {
            IngestUrl = _defaultStreamingOptions.IngestUrl,
            RecordingOutputPath = _defaultStreamingOptions.RecordingOutputPath,
            StartWithHost = _defaultStreamingOptions.StartWithHost,
            BrbSlatePath = _defaultStreamingOptions.BrbSlatePath
        };
        _rtmpServerClient.Configure(_defaultRtmpServerOptions.BaseUrl, _defaultRtmpServerOptions.ApiKey);
    }

    public RestreamConfigurationSnapshot GetCurrent()
    {
        lock (_configurationLock)
        {
            return _currentConfiguration;
        }
    }

    public Task<RestreamControlState> GetState(CancellationToken cancellationToken = default)
    {
        return BuildState(cancellationToken);
    }

    public async Task<RestreamControlState> UpdateConfiguration(
        RestreamConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string ingestUrl = request.IngestUrl.Trim();
        string recordingOutputPath = request.RecordingOutputPath.Trim();
        string brbSlatePath = request.BrbSlatePath.Trim();

        if (string.IsNullOrWhiteSpace(ingestUrl))
        {
            throw new ArgumentException("A restream ingest URL is required.", nameof(request));
        }

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        RestreamConfiguration? configuration = await dbContext.RestreamConfigurations
            .SingleOrDefaultAsync(cancellationToken);

        if (configuration is null)
        {
            configuration = new RestreamConfiguration();
            dbContext.RestreamConfigurations.Add(configuration);
        }

        configuration.IngestUrl = ingestUrl;
        configuration.RecordingOutputPath = recordingOutputPath;
        configuration.StartWithHost = request.StartWithHost;
        configuration.BrbSlatePath = brbSlatePath;
        configuration.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.RtmpServerUrl))
        {
            configuration.RtmpServerUrl = request.RtmpServerUrl.Trim();
            configuration.RtmpServerApiKey = request.RtmpServerApiKey.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _rtmpServerClient.Configure(configuration.RtmpServerUrl, configuration.RtmpServerApiKey);

        _logger.LogInformation(
            "Updated restream configuration: ingest={IngestUrl}, recordingPathConfigured={HasRecordingPath}, startWithHost={StartWithHost}, brbSlateConfigured={HasBrbSlate}",
            ingestUrl,
            !string.IsNullOrWhiteSpace(recordingOutputPath),
            request.StartWithHost,
            !string.IsNullOrWhiteSpace(brbSlatePath));

        return await BuildState(cancellationToken, "Restream configuration saved.");
    }

    public async Task<RestreamControlState> PushConfiguration(CancellationToken cancellationToken = default)
    {
        RestreamConfigurationSnapshot snapshot = GetCurrent();
        IReadOnlyList<RtmpRelayDestination> destinations = await GetActiveDestinations(cancellationToken);

        RtmpServerConfigurationPush push = new RtmpServerConfigurationPush(
            IngestUrl: snapshot.IngestUrl,
            RecordingOutputPath: snapshot.RecordingOutputPath,
            BrbSlatePath: snapshot.BrbSlatePath,
            Destinations: destinations
                .Select(static d => new RtmpRelayDestinationPush(d.PlatformName, d.DestinationUrl))
                .ToArray());

        await _rtmpServerClient.PushConfiguration(push, cancellationToken);

        _logger.LogInformation(
            "Pushed restream configuration to the RTMP server with {ActiveDestinationCount} active destination(s).",
            destinations.Count);

        return await BuildState(cancellationToken, "Restream configuration pushed to the RTMP server.");
    }

    public async Task<RestreamControlState> UpdateDestination(RestreamDestinationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IIntegrationConnectionMonitor monitor = ResolveMonitor(request.PlatformName);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        RestreamDestinationConfiguration? configuration = await dbContext.RestreamDestinationConfigurations
            .SingleOrDefaultAsync(
                destination => destination.PlatformName == monitor.PlatformName,
                cancellationToken);

        if (configuration is null)
        {
            configuration = new RestreamDestinationConfiguration
            {
                PlatformName = monitor.PlatformName
            };

            dbContext.RestreamDestinationConfigurations.Add(configuration);
        }

        configuration.IsEnabled = request.IsEnabled;
        configuration.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated restream destination {PlatformName}: enabled={IsEnabled}",
            monitor.PlatformName,
            request.IsEnabled);

        return await BuildState(
            cancellationToken,
            request.IsEnabled
                ? $"{monitor.PlatformName} is armed for the next restream start."
                : $"{monitor.PlatformName} is excluded from fanout.");
    }

    public async Task<RestreamControlState> Start(CancellationToken cancellationToken = default)
    {
        RestreamControlState state = await BuildState(cancellationToken);
        if (!state.CanStart)
        {
            return state with
            {
                OperatorMessage = "Enable at least one connected destination before starting restreaming."
            };
        }

        RestreamConfigurationSnapshot snapshot = GetCurrent();
        IReadOnlyList<RtmpRelayDestination> destinations = await GetActiveDestinations(cancellationToken);

        RtmpServerConfigurationPush push = new RtmpServerConfigurationPush(
            IngestUrl: snapshot.IngestUrl,
            RecordingOutputPath: snapshot.RecordingOutputPath,
            BrbSlatePath: snapshot.BrbSlatePath,
            Destinations: destinations
                .Select(static d => new RtmpRelayDestinationPush(d.PlatformName, d.DestinationUrl))
                .ToArray());

        await _rtmpServerClient.PushConfiguration(push, cancellationToken);

        await _streamingService.Start(cancellationToken);
        await _fanoutService.StartFanout(cancellationToken);

        _logger.LogInformation(
            "Started restream runtime with {ActiveDestinationCount} active destination(s).",
            state.ActiveDestinationCount);

        return await BuildState(cancellationToken, "Restream ingest and fanout are marked as running.");
    }

    public async Task<RestreamControlState> Stop(CancellationToken cancellationToken = default)
    {
        await _fanoutService.StopFanout(cancellationToken);
        await _streamingService.Stop(cancellationToken);

        _logger.LogInformation("Stopped restream runtime.");

        return await BuildState(cancellationToken, "Restream ingest and fanout are marked as stopped.");
    }

    private async Task<RestreamControlState> BuildState(
        CancellationToken cancellationToken,
        string operatorMessage = "")
    {
        await RefreshMonitors(cancellationToken);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        RestreamConfiguration? persistedConfiguration = await dbContext.RestreamConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        Dictionary<string, RestreamDestinationConfiguration> destinationConfigurations = await dbContext.RestreamDestinationConfigurations
            .AsNoTracking()
            .ToDictionaryAsync(
                destination => destination.PlatformName,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        List<RestreamDestinationState> destinations = new List<RestreamDestinationState>(_integrationMonitors.Count);
        foreach (IIntegrationConnectionMonitor monitor in _integrationMonitors)
        {
            bool isEnabled = destinationConfigurations.TryGetValue(monitor.PlatformName, out RestreamDestinationConfiguration? destinationConfiguration) &&
                destinationConfiguration.IsEnabled;
            bool supportsRelay = _relayProviders.TryGetValue(monitor.PlatformName, out IRtmpRelayDestinationProvider? relayProvider);
            RtmpRelayDestination? relayDestination = supportsRelay
                ? await relayProvider!.GetRelayDestination(cancellationToken)
                : null;
            string relayStatus = relayDestination is not null
                ? "Relay destination ready."
                : supportsRelay
                    ? "Relay destination not configured."
                    : "Adapter does not expose a relay destination yet.";

            destinations.Add(
                new RestreamDestinationState
                {
                    PlatformName = monitor.PlatformName,
                    IsConnected = monitor.IsConnected,
                    IsEnabled = isEnabled,
                    SupportsRelay = supportsRelay,
                    IsRelayConfigured = relayDestination is not null,
                    RelayStatus = relayStatus
                });
        }

        string ingestUrl = persistedConfiguration?.IngestUrl ?? _defaultStreamingOptions.IngestUrl;
        string recordingOutputPath = persistedConfiguration?.RecordingOutputPath ?? _defaultStreamingOptions.RecordingOutputPath;
        bool startWithHost = persistedConfiguration?.StartWithHost ?? _defaultStreamingOptions.StartWithHost;
        string brbSlatePath = persistedConfiguration?.BrbSlatePath ?? _defaultStreamingOptions.BrbSlatePath;
        string rtmpServerUrl = string.IsNullOrWhiteSpace(persistedConfiguration?.RtmpServerUrl)
            ? _defaultRtmpServerOptions.BaseUrl
            : persistedConfiguration.RtmpServerUrl;
        string rtmpServerApiKey = string.IsNullOrWhiteSpace(persistedConfiguration?.RtmpServerApiKey)
            ? _defaultRtmpServerOptions.ApiKey
            : persistedConfiguration.RtmpServerApiKey;
        _rtmpServerClient.Configure(rtmpServerUrl, rtmpServerApiKey);
        StreamRecordingSnapshot? latestRecording = await _streamRecordingService.GetLatest(LocalRecordingPlatform, cancellationToken);

        IReadOnlyList<RestreamDestinationSnapshot> destinationSnapshots =
        [
            .. destinations.Select(destination => new RestreamDestinationSnapshot
            {
                PlatformName = destination.PlatformName,
                IsEnabled = destination.IsEnabled,
                IsAvailable = destination.IsConnected,
                IsRelayConfigured = destination.IsRelayConfigured,
                ConnectionState = destination.IsConnected ? "Connected" : "Not connected",
                RelayStatus = destination.RelayStatus
            })
        ];

        lock (_configurationLock)
        {
            _currentConfiguration = new RestreamConfigurationSnapshot
            {
                IngestUrl = ingestUrl,
                RecordingOutputPath = recordingOutputPath,
                StartWithHost = startWithHost,
                BrbSlatePath = brbSlatePath,
                Destinations = destinationSnapshots
            };
        }

        int enabledDestinationCount = destinations.Count(static destination => destination.IsEnabled);
        int connectedDestinationCount = destinations.Count(static destination => destination.IsConnected);
        int activeDestinationCount = destinations.Count(static destination => destination.IsEnabled && destination.IsConnected && destination.IsRelayConfigured);

        return new RestreamControlState
        {
            IngestUrl = ingestUrl,
            RecordingOutputPath = recordingOutputPath,
            StartWithHost = startWithHost,
            BrbSlatePath = brbSlatePath,
            IsBrbSlateConfigured = !string.IsNullOrWhiteSpace(brbSlatePath),
            IsIngestRunning = _streamingService.IsRunning,
            IsFanoutRunning = _fanoutService.IsRunning,
            IsRecording = latestRecording is { EndedAt: null },
            EnabledDestinationCount = enabledDestinationCount,
            ConnectedDestinationCount = connectedDestinationCount,
            ActiveDestinationCount = activeDestinationCount,
            CanStart = activeDestinationCount > 0 && _rtmpServerClient.IsConnected,
            OperatorMessage = operatorMessage,
            DependencyNote = DependencyNote,
            LatestRecording = latestRecording,
            Destinations = destinations,
            IsRtmpServerConnected = _rtmpServerClient.IsConnected,
            RtmpServerUrl = rtmpServerUrl
        };
    }

    private async Task<IReadOnlyList<RtmpRelayDestination>> GetActiveDestinations(CancellationToken cancellationToken)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        Dictionary<string, bool> enabledByPlatform = await dbContext.RestreamDestinationConfigurations
            .AsNoTracking()
            .ToDictionaryAsync(
                static d => d.PlatformName,
                static d => d.IsEnabled,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        List<RtmpRelayDestination> results = new List<RtmpRelayDestination>();
        foreach (KeyValuePair<string, IRtmpRelayDestinationProvider> entry in _relayProviders)
        {
            if (!enabledByPlatform.TryGetValue(entry.Key, out bool isEnabled) || !isEnabled)
            {
                continue;
            }

            RtmpRelayDestination? destination = await entry.Value.GetRelayDestination(cancellationToken);
            if (destination is not null)
            {
                results.Add(destination);
            }
        }

        return results;
    }

    private async Task RefreshMonitors(CancellationToken cancellationToken)
    {
        foreach (IIntegrationConnectionMonitor monitor in _integrationMonitors)
        {
            await monitor.RefreshConnectionState(cancellationToken);
        }
    }

    private IIntegrationConnectionMonitor ResolveMonitor(string platformName)
    {
        if (string.IsNullOrWhiteSpace(platformName))
        {
            throw new ArgumentException("A platform name is required.", nameof(platformName));
        }

        IIntegrationConnectionMonitor? monitor = _integrationMonitors
            .FirstOrDefault(current => string.Equals(current.PlatformName, platformName.Trim(), StringComparison.OrdinalIgnoreCase));

        return monitor ?? throw new ArgumentException(
            $"Restream destination '{platformName}' is not registered.",
            nameof(platformName));
    }
}
