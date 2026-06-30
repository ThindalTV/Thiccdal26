using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.RtmpServer.Services;

namespace Thiccdal.Data.Tests;

public sealed class RtmpFanoutServiceTests
{
    [Fact]
    public async Task WhenFanoutStartsWhileStreamingLive_ThenAllConfiguredDestinationsStartLiveRelays()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelaySessionFactory relaySessionFactory = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            CreateHolder("Null", "TikTok"));

        await service.StartFanout();

        Assert.True(service.IsRunning);
        Assert.Equal(2, relaySessionFactory.LiveRelayStarts.Count);
        Assert.Equal("Null", relaySessionFactory.LiveRelayStarts[0].PlatformName);
        Assert.Equal("TikTok", relaySessionFactory.LiveRelayStarts[1].PlatformName);
        Assert.All(relaySessionFactory.LiveRelayStarts, start => Assert.Equal("rtmp://localhost:1935/live", start.SourceUrl));
    }

    [Fact]
    public async Task WhenFanoutStops_ThenAllLiveRelaySessionsAreStoppedAndDisposed()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelaySessionFactory relaySessionFactory = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            CreateHolder("Null", "TikTok"));

        await service.StartFanout();
        await service.StopFanout();

        Assert.False(service.IsRunning);
        Assert.All(relaySessionFactory.CreatedSessions, session => Assert.Equal(1, session.StopCount));
        Assert.All(relaySessionFactory.CreatedSessions, session => Assert.Equal(1, session.DisposeCount));
    }

    [Fact]
    public async Task WhenOneRelayStartThrows_ThenOtherTargetsContinueAndErrorIsLogged()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelaySessionFactory relaySessionFactory = new();
        relaySessionFactory.PlatformsThatThrowOnStart.Add("TikTok");
        RecordingLogger<RtmpFanoutService> logger = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            new FakeBrbSlateInjector(),
            CreateHolder("Null", "TikTok"),
            logger);

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
    public async Task WhenStreamingTransitionsToBrbSlate_ThenLiveRelaysStopAndBrbStarts()
    {
        FakeStreamingService streamingService = new(StreamingState.Live);
        FakeRelaySessionFactory relaySessionFactory = new();
        FakeBrbSlateInjector brbSlateInjector = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            brbSlateInjector,
            CreateHolder("Null", "TikTok"));

        await service.StartFanout();

        streamingService.SetState(StreamingState.BrbSlate);
        await WaitFor(() => brbSlateInjector.StartCount == 1);

        Assert.All(relaySessionFactory.CreatedSessions, session => Assert.Equal(1, session.StopCount));
        Assert.Equal(2, brbSlateInjector.LastDestinations.Count);
    }

    [Fact]
    public async Task WhenStreamingReconnectsToLive_ThenBrbStopsAndLiveRelaysRestart()
    {
        FakeStreamingService streamingService = new(StreamingState.BrbSlate);
        FakeRelaySessionFactory relaySessionFactory = new();
        FakeBrbSlateInjector brbSlateInjector = new();
        RtmpFanoutService service = CreateService(
            streamingService,
            relaySessionFactory,
            brbSlateInjector,
            CreateHolder("Null", "TikTok"));

        await service.StartFanout();
        Assert.Equal(1, brbSlateInjector.StartCount);

        streamingService.SetState(StreamingState.Live);
        await WaitFor(() => relaySessionFactory.LiveRelayStarts.Count == 2);

        Assert.Equal(1, brbSlateInjector.StopCount);
        Assert.Equal(2, relaySessionFactory.CreatedSessions.Count);
    }

    private static FakeConfigurationHolder CreateHolder(params string[] platformNames)
    {
        return new FakeConfigurationHolder(
            new RtmpServerConfigurationPush(
                "rtmp://localhost:1935/live",
                string.Empty,
                string.Empty,
                platformNames
                    .Select(static name => new RtmpRelayDestinationPush(
                        name,
                        $"rtmp://localhost:1936/live/{name.ToLowerInvariant()}"))
                    .ToArray()));
    }

    private static RtmpFanoutService CreateService(
        FakeStreamingService streamingService,
        FakeRelaySessionFactory relaySessionFactory,
        FakeBrbSlateInjector brbSlateInjector,
        FakeConfigurationHolder holder)
    {
        return CreateService(
            streamingService,
            relaySessionFactory,
            brbSlateInjector,
            holder,
            NullLogger<RtmpFanoutService>.Instance);
    }

    private static RtmpFanoutService CreateService(
        FakeStreamingService streamingService,
        FakeRelaySessionFactory relaySessionFactory,
        FakeBrbSlateInjector brbSlateInjector,
        FakeConfigurationHolder holder,
        ILogger<RtmpFanoutService> logger)
    {
        return new RtmpFanoutService(
            holder,
            streamingService,
            relaySessionFactory,
            brbSlateInjector,
            new FakeRtmpEventPublisher(),
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

    private sealed class FakeConfigurationHolder : IRtmpServerConfigurationHolder
    {
        private RtmpServerConfigurationPush _current;

        public FakeConfigurationHolder(RtmpServerConfigurationPush initial)
        {
            _current = initial;
        }

        public RtmpServerConfigurationPush GetCurrent() => _current;

        public void Apply(RtmpServerConfigurationPush configuration)
        {
            _current = configuration;
        }
    }

    private sealed class FakeRtmpEventPublisher : IRtmpEventPublisher
    {
        public Task PublishIngestConnected(string streamPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishIngestDisconnected(string streamPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishIngestError(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishRecordingStarted(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishRecordingEnded(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishRelayFailed(string platformName, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

