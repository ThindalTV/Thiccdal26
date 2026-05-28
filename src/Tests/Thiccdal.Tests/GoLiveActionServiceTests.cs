using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Tests;

public sealed class GoLiveActionServiceTests
{
    [Fact]
    public async Task WhenGoLiveSucceeds_ThenStreamingStartsBeforeFanoutAndChecklistResets()
    {
        using OperatorStateService operatorStateService = new();
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        StubChecklistService checklistService = new(allRequiredChecked: true);
        RecordingStreamingService streamingService = new();
        RecordingChecklistSessionService checklistSessionService = new(streamingService.Events);
        RecordingFanoutService fanoutService = new(streamingService.Events);
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 18, 30, 0, TimeSpan.Zero));
        GoLiveActionService service = CreateService(streamingService, fanoutService, operatorStateService, checklistService, checklistSessionService, timeProvider);

        await service.Execute();

        Assert.Equal(["checklist.save", "streaming.start", "fanout.start"], streamingService.Events);
        Assert.Equal(OperatorMode.Live, operatorStateService.Mode);
        Assert.NotEqual(Guid.Empty, checklistSessionService.SessionId);
        Assert.Equal(checklistSessionService.SessionId, checklistService.SessionId);
        Assert.True(checklistService.HandleGoLiveSucceededCalled);
        Assert.True(checklistService.ResetCalled);
        Assert.Equal(timeProvider.GetUtcNow(), checklistService.StartedAt);
        Assert.Equal(timeProvider.GetUtcNow(), operatorStateService.GetActiveStreamState()?.StartedAt);
        Assert.Equal(checklistSessionService.SessionId, operatorStateService.GetActiveStreamState()?.SessionId);
        Assert.Null(service.GetState().ErrorMessage);
    }

    [Fact]
    public async Task WhenFanoutFails_ThenModeStaysPreLiveAndCleanupRuns()
    {
        using OperatorStateService operatorStateService = new();
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        StubChecklistService checklistService = new(allRequiredChecked: true);
        RecordingStreamingService streamingService = new();
        RecordingChecklistSessionService checklistSessionService = new(streamingService.Events);
        RecordingFanoutService fanoutService = new(streamingService.Events, failOnStart: true);
        GoLiveActionService service = CreateService(streamingService, fanoutService, operatorStateService, checklistService, checklistSessionService);

        await service.Execute();

        Assert.Equal(OperatorMode.PreLive, operatorStateService.Mode);
        Assert.False(checklistService.HandleGoLiveSucceededCalled);
        Assert.False(checklistService.ResetCalled);
        Assert.Equal(["checklist.save", "streaming.start", "fanout.start", "streaming.stop"], streamingService.Events);
        Assert.Equal("Go live failed: Fanout failed.", service.GetState().ErrorMessage);
    }

    [Fact]
    public async Task WhenGoLiveTimesOut_ThenCleanupRunsAndErrorStateIsSet()
    {
        using OperatorStateService operatorStateService = new();
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        StubChecklistService checklistService = new(allRequiredChecked: true);
        RecordingStreamingService streamingService = new(delayOnStart: TimeSpan.FromMilliseconds(80));
        RecordingChecklistSessionService checklistSessionService = new(streamingService.Events);
        RecordingFanoutService fanoutService = new(streamingService.Events);
        GoLiveActionService service = CreateService(
            streamingService,
            fanoutService,
            operatorStateService,
            checklistService,
            checklistSessionService,
            timeout: TimeSpan.FromMilliseconds(25));

        await service.Execute();

        Assert.Equal(OperatorMode.PreLive, operatorStateService.Mode);
        Assert.False(checklistService.HandleGoLiveSucceededCalled);
        Assert.Contains("Go live timed out", service.GetState().ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(["checklist.save", "streaming.start", "streaming.stop"], streamingService.Events);
    }

    private static GoLiveActionService CreateService(
        IStreamingService streamingService,
        IRtmpFanoutService fanoutService,
        IOperatorStateService operatorStateService,
        IPreLiveChecklistService checklistService,
        IChecklistSessionService checklistSessionService,
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null)
    {
        return timeout is null
            ? new GoLiveActionService(
                streamingService,
                fanoutService,
                operatorStateService,
                checklistService,
                checklistSessionService,
                timeProvider ?? TimeProvider.System,
                NullLogger<GoLiveActionService>.Instance)
            : new GoLiveActionService(
                streamingService,
                fanoutService,
                operatorStateService,
                checklistService,
                checklistSessionService,
                timeProvider ?? TimeProvider.System,
                NullLogger<GoLiveActionService>.Instance,
                timeout.Value);
    }

    private sealed class StubChecklistService : IPreLiveChecklistService
    {
        private readonly bool _allRequiredChecked;

        public StubChecklistService(bool allRequiredChecked)
        {
            _allRequiredChecked = allRequiredChecked;
        }

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public bool AllRequiredChecked => _allRequiredChecked;

        public int RequiredUncheckedCount => _allRequiredChecked ? 0 : 1;

        public int OptionalUncheckedCount => 0;

        public bool HandleGoLiveSucceededCalled { get; private set; }

        public bool ResetCalled { get; private set; }

        public DateTimeOffset? StartedAt { get; private set; }

        public Guid? SessionId { get; private set; }

        public PreLiveChecklistState GetState()
        {
            return new PreLiveChecklistState
            {
                AllRequiredChecked = _allRequiredChecked
            };
        }

        public void SetItemChecked(string itemId, bool isChecked)
        {
            _ = itemId;
            _ = isChecked;
        }

        public Task TriggerAction(string itemId, CancellationToken cancellationToken = default)
        {
            _ = itemId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Reload(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public void Reset()
        {
            ResetCalled = true;
        }

        public void HandleGoLiveSucceeded(DateTimeOffset? startedAt = null, Guid? sessionId = null)
        {
            HandleGoLiveSucceededCalled = true;
            StartedAt = startedAt;
            SessionId = sessionId;
        }
    }

    private sealed class RecordingChecklistSessionService : IChecklistSessionService
    {
        private readonly List<string> _events;

        public RecordingChecklistSessionService(List<string> events)
        {
            _events = events;
        }

        public Guid SessionId { get; private set; }

        public Task Save(Guid sessionId, IPreLiveChecklistService checklist, CancellationToken cancellationToken = default)
        {
            _ = checklist;
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add("checklist.save");
            SessionId = sessionId;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStreamingService : IStreamingService
    {
        private readonly TimeSpan? _delayOnStart;

        public RecordingStreamingService(TimeSpan? delayOnStart = null)
        {
            _delayOnStart = delayOnStart;
        }

        public List<string> Events { get; } = [];

        public bool IsRunning { get; private set; }

        public StreamingState State { get; private set; }

        public event EventHandler<StreamingState>? StateChanged;

        public async Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null)
        {
            _ = sessionId;
            Events.Add("streaming.start");
            if (_delayOnStart is not null)
            {
                await Task.Delay(_delayOnStart.Value, cancellationToken);
            }

            IsRunning = true;
            State = StreamingState.Live;
            StateChanged?.Invoke(this, State);
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            Events.Add("streaming.stop");
            IsRunning = false;
            State = StreamingState.Idle;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFanoutService : IRtmpFanoutService
    {
        private readonly List<string> _events;
        private readonly bool _failOnStart;

        public RecordingFanoutService(List<string> events, bool failOnStart = false)
        {
            _events = events;
            _failOnStart = failOnStart;
        }

        public bool IsRunning { get; private set; }

        public Task StartFanout(CancellationToken cancellationToken = default)
        {
            _events.Add("fanout.start");
            if (_failOnStart)
            {
                throw new InvalidOperationException("Fanout failed.");
            }

            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopFanout(CancellationToken cancellationToken = default)
        {
            _events.Add("fanout.stop");
            IsRunning = false;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
