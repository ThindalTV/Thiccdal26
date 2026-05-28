using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Streaming;

namespace Thiccdal.Tests;

public sealed class RtmpFanoutServiceTests
{
    [Fact]
    public async Task WhenFanoutStartsWhileStreamingLive_ThenAllEnabledConnectedDestinationsStartLiveRelays()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelayTarget nullTarget = new("Null");
        FakeRelayTarget tikTokTarget = new("TikTok");
        FakeRelaySessionFactory relaySessionFactory = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            new FakeRestreamSettingsAccessor(
                CreateEnabledDestination("Null"),
                CreateEnabledDestination("TikTok")),
            nullTarget,
            tikTokTarget);

        await service.StartFanout();

        Assert.True(service.IsRunning);
        Assert.Equal(2, relaySessionFactory.LiveRelayStarts.Count);
        Assert.Equal("Null", relaySessionFactory.LiveRelayStarts[0].PlatformName);
        Assert.Equal("TikTok", relaySessionFactory.LiveRelayStarts[1].PlatformName);
        Assert.All(relaySessionFactory.LiveRelayStarts, start => Assert.Equal("rtmp://localhost:1935/live", start.SourceUrl));
    }

    [Fact]
    public async Task WhenFanoutStarts_ThenPendingApprovalAndDisabledTargetsAreSkipped()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelayTarget connectedTarget = new("Null");
        FakeRelayTarget pendingTarget = new("TikTok", PlatformConnectionState.PendingApproval);
        FakeRelayTarget disabledTarget = new("LinkedIn", PlatformConnectionState.Disabled);
        FakeRelaySessionFactory relaySessionFactory = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            new FakeRestreamSettingsAccessor(
                CreateEnabledDestination("Null"),
                CreateEnabledDestination("TikTok"),
                CreateEnabledDestination("LinkedIn")),
            connectedTarget,
            pendingTarget,
            disabledTarget);

        await service.StartFanout();

        Assert.Single(relaySessionFactory.LiveRelayStarts);
        Assert.Equal("Null", relaySessionFactory.LiveRelayStarts[0].PlatformName);
    }

    [Fact]
    public async Task WhenFanoutStarts_ThenDisconnectedAndErroredTargetsAreSkipped()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelayTarget connectedTarget = new("Null");
        FakeRelayTarget disconnectedTarget = new("TikTok", PlatformConnectionState.Disconnected);
        FakeRelayTarget erroredTarget = new("LinkedIn", PlatformConnectionState.Error);
        FakeRelaySessionFactory relaySessionFactory = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            new FakeRestreamSettingsAccessor(
                CreateEnabledDestination("Null"),
                CreateEnabledDestination("TikTok"),
                CreateEnabledDestination("LinkedIn")),
            connectedTarget,
            disconnectedTarget,
            erroredTarget);

        await service.StartFanout();

        Assert.Single(relaySessionFactory.LiveRelayStarts);
        Assert.Equal("Null", relaySessionFactory.LiveRelayStarts[0].PlatformName);
    }

    [Fact]
    public async Task WhenFanoutStarts_ThenOperatorDisabledDestinationsAreSkipped()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelayTarget nullTarget = new("Null");
        FakeRelayTarget tikTokTarget = new("TikTok");
        FakeRelaySessionFactory relaySessionFactory = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            new FakeRestreamSettingsAccessor(
                CreateEnabledDestination("Null"),
                CreateDisabledDestination("TikTok")),
            nullTarget,
            tikTokTarget);

        await service.StartFanout();

        Assert.Single(relaySessionFactory.LiveRelayStarts);
        Assert.Equal("Null", relaySessionFactory.LiveRelayStarts[0].PlatformName);
    }

    [Fact]
    public async Task WhenOneRelayStartThrows_ThenOtherTargetsContinueAndErrorIsLogged()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelayTarget nullTarget = new("Null");
        FakeRelayTarget tikTokTarget = new("TikTok");
        FakeRelaySessionFactory relaySessionFactory = new();
        relaySessionFactory.PlatformsThatThrowOnStart.Add("TikTok");
        RecordingLogger<RtmpFanoutService> logger = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            new FakeRestreamSettingsAccessor(
                CreateEnabledDestination("Null"),
                CreateEnabledDestination("TikTok")),
            logger,
            nullTarget,
            tikTokTarget);

        await service.StartFanout();

        Assert.Equal(2, relaySessionFactory.LiveRelayStarts.Count);
        Assert.Single(relaySessionFactory.CreatedSessions);
        Assert.Equal("Null", relaySessionFactory.CreatedSessions[0].PlatformName);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Error &&
                     entry.Message.Contains("TikTok", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenFanoutStops_ThenAllLiveRelaySessionsAreStoppedAndDisposed()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelayTarget nullTarget = new("Null");
        FakeRelayTarget tikTokTarget = new("TikTok");
        FakeRelaySessionFactory relaySessionFactory = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            new FakeRestreamSettingsAccessor(
                CreateEnabledDestination("Null"),
                CreateEnabledDestination("TikTok")),
            nullTarget,
            tikTokTarget);

        await service.StartFanout();
        await service.StopFanout();

        Assert.False(service.IsRunning);
        Assert.All(relaySessionFactory.CreatedSessions, session => Assert.Equal(1, session.StopCount));
        Assert.All(relaySessionFactory.CreatedSessions, session => Assert.Equal(1, session.DisposeCount));
    }

    [Fact]
    public async Task WhenStreamingTransitionsToBrbSlate_ThenLiveRelaysStopAndBrbStarts()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelayTarget nullTarget = new("Null");
        FakeRelayTarget tikTokTarget = new("TikTok");
        FakeRelaySessionFactory relaySessionFactory = new();
        FakeBrbSlateInjector brbSlateInjector = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            brbSlateInjector,
            new FakeRestreamSettingsAccessor(
                CreateEnabledDestination("Null"),
                CreateEnabledDestination("TikTok")),
            nullTarget,
            tikTokTarget);

        await service.StartFanout();

        streamingService.SetState(StreamingState.BrbSlate);
        await WaitFor(() => brbSlateInjector.StartCount == 1);

        Assert.All(relaySessionFactory.CreatedSessions, session => Assert.Equal(1, session.StopCount));
        Assert.Equal(2, brbSlateInjector.LastDestinations.Count);
        Assert.Contains(brbSlateInjector.LastDestinations, destination => destination.PlatformName == "Null");
        Assert.Contains(brbSlateInjector.LastDestinations, destination => destination.PlatformName == "TikTok");
    }

    [Fact]
    public async Task WhenStreamingReconnectsToLive_ThenBrbStopsAndLiveRelaysRestart()
    {
        FakeStreamingService streamingService = new(StreamingState.BrbSlate);
        FakeRelayTarget nullTarget = new("Null");
        FakeRelayTarget tikTokTarget = new("TikTok");
        FakeRelaySessionFactory relaySessionFactory = new();
        FakeBrbSlateInjector brbSlateInjector = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            brbSlateInjector,
            new FakeRestreamSettingsAccessor(
                CreateEnabledDestination("Null"),
                CreateEnabledDestination("TikTok")),
            nullTarget,
            tikTokTarget);

        await service.StartFanout();
        Assert.Equal(1, brbSlateInjector.StartCount);

        streamingService.SetState(StreamingState.Live);
        await WaitFor(() => relaySessionFactory.LiveRelayStarts.Count == 2);

        Assert.Equal(1, brbSlateInjector.StopCount);
        Assert.Equal(2, relaySessionFactory.CreatedSessions.Count);
    }

    private static RestreamDestinationSnapshot CreateEnabledDestination(string platformName)
    {
        return new RestreamDestinationSnapshot
        {
            PlatformName = platformName,
            IsEnabled = true,
            IsAvailable = true,
            IsRelayConfigured = true,
            ConnectionState = "Connected",
            RelayStatus = "Relay destination ready."
        };
    }

    private static RestreamDestinationSnapshot CreateDisabledDestination(string platformName)
    {
        return new RestreamDestinationSnapshot
        {
            PlatformName = platformName,
            IsEnabled = false,
            IsAvailable = true,
            IsRelayConfigured = true,
            ConnectionState = "Connected",
            RelayStatus = "Relay destination ready."
        };
    }

    private static RtmpFanoutService CreateService(
        FakeStreamingService streamingService,
        FakeRelaySessionFactory relaySessionFactory,
        FakeBrbSlateInjector brbSlateInjector,
        FakeRestreamSettingsAccessor settingsAccessor,
        params FakeRelayTarget[] relayTargets)
    {
        return CreateService(
            streamingService,
            relaySessionFactory,
            brbSlateInjector,
            settingsAccessor,
            NullLogger<RtmpFanoutService>.Instance,
            relayTargets);
    }

    private static RtmpFanoutService CreateService(
        FakeStreamingService streamingService,
        FakeRelaySessionFactory relaySessionFactory,
        FakeBrbSlateInjector brbSlateInjector,
        FakeRestreamSettingsAccessor settingsAccessor,
        ILogger<RtmpFanoutService> logger,
        params FakeRelayTarget[] relayTargets)
    {
        return new RtmpFanoutService(
            relayTargets,
            relayTargets,
            settingsAccessor,
            streamingService,
            relaySessionFactory,
            brbSlateInjector,
            logger);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private sealed class FakeRestreamSettingsAccessor : IRestreamSettingsAccessor
    {
        private readonly RestreamConfigurationSnapshot _snapshot;

        public FakeRestreamSettingsAccessor(params RestreamDestinationSnapshot[] destinations)
        {
            _snapshot = new RestreamConfigurationSnapshot
            {
                IngestUrl = "rtmp://localhost:1935/live",
                Destinations = destinations
            };
        }

        public RestreamConfigurationSnapshot GetCurrent()
        {
            return _snapshot;
        }
    }

    private sealed class FakeStreamingService : IStreamingService
    {
        public FakeStreamingService(StreamingState initialState)
        {
            State = initialState;
        }

        public bool IsRunning => State != StreamingState.Idle;

        public StreamingState State { get; private set; }

        public event EventHandler<StreamingState>? StateChanged;

        public Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null)
        {
            _ = sessionId;
            cancellationToken.ThrowIfCancellationRequested();
            State = StreamingState.WaitingForIngest;
            return Task.CompletedTask;
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = StreamingState.Idle;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public void SetState(StreamingState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }

    private sealed class FakeRelayTarget : IPlatformConnection, IRtmpRelayDestinationProvider
    {
        public FakeRelayTarget(string platformName, PlatformConnectionState state = PlatformConnectionState.Connected)
        {
            PlatformName = platformName;
            State = state;
        }

        public string PlatformName { get; }

        public PlatformConnectionState State { get; }

        public string? LastError => null;

        public bool Connected => State == PlatformConnectionState.Connected;

        public event EventHandler<ChatEvent>? OnChatMessageRecieved
        {
            add { }
            remove { }
        }

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived
        {
            add { }
            remove { }
        }

        public Task Connect(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            _ = message;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<RtmpRelayDestination?> GetRelayDestination(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<RtmpRelayDestination?>(
                new RtmpRelayDestination
                {
                    PlatformName = PlatformName,
                    DestinationUrl = $"rtmp://localhost:1936/live/{PlatformName.ToLowerInvariant()}"
                });
        }
    }

    private sealed class FakeBrbSlateInjector : IBrbSlateInjector
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public bool IsRunning { get; private set; }

        public IReadOnlyList<RtmpRelayDestination> LastDestinations { get; private set; } = Array.Empty<RtmpRelayDestination>();

        public Task Start(IReadOnlyList<RtmpRelayDestination> destinations, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            IsRunning = true;
            LastDestinations = destinations.ToArray();
            return Task.CompletedTask;
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsRunning)
            {
                StopCount++;
            }

            IsRunning = false;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRelaySessionFactory : IStreamingRelaySessionFactory
    {
        public List<LiveRelayStart> LiveRelayStarts { get; } = [];

        public List<FakeRelaySession> CreatedSessions { get; } = [];

        public HashSet<string> PlatformsThatThrowOnStart { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IStreamingRelaySession> StartLiveRelay(
            string platformName,
            string sourceUrl,
            string destinationUrl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiveRelayStarts.Add(new LiveRelayStart(platformName, sourceUrl, destinationUrl));

            if (PlatformsThatThrowOnStart.Contains(platformName))
            {
                throw new InvalidOperationException($"Relay startup failed for {platformName}.");
            }

            FakeRelaySession session = new(platformName);
            CreatedSessions.Add(session);
            return Task.FromResult<IStreamingRelaySession>(session);
        }

        public Task<IStreamingRelaySession> StartBrbRelay(
            string platformName,
            string slatePath,
            string destinationUrl,
            CancellationToken cancellationToken = default)
        {
            _ = platformName;
            _ = slatePath;
            _ = destinationUrl;
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("BRB relay creation should be handled by the injector fake.");
        }
    }

    private sealed class FakeRelaySession : IStreamingRelaySession
    {
        public FakeRelaySession(string platformName)
        {
            PlatformName = platformName;
        }

        public string PlatformName { get; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed record LiveRelayStart(string PlatformName, string SourceUrl, string DestinationUrl);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            _ = state;
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _ = eventId;
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
