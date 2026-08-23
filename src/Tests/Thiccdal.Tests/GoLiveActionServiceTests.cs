using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Tests;

public sealed class GoLiveActionServiceTests
{
    [Fact]
    public async Task WhenGoLiveSucceeds_ThenChecklistIsSavedAndLiveSessionBegins()
    {
        using OperatorStateService operatorStateService = new();
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        StubChecklistService checklistService = new(allRequiredChecked: true);
        RecordingChecklistSessionService checklistSessionService = new();
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 18, 30, 0, TimeSpan.Zero));
        GoLiveActionService service = CreateService(operatorStateService, checklistService, checklistSessionService, timeProvider);

        await service.Execute();

        Assert.Equal(["checklist.save"], checklistSessionService.Events);
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
    public async Task WhenChecklistSaveFails_ThenModeStaysPreLiveAndErrorStateIsSet()
    {
        using OperatorStateService operatorStateService = new();
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        StubChecklistService checklistService = new(allRequiredChecked: true);
        RecordingChecklistSessionService checklistSessionService = new(failOnSave: true);
        GoLiveActionService service = CreateService(operatorStateService, checklistService, checklistSessionService);

        await service.Execute();

        Assert.Equal(OperatorMode.PreLive, operatorStateService.Mode);
        Assert.False(checklistService.HandleGoLiveSucceededCalled);
        Assert.False(checklistService.ResetCalled);
        Assert.Equal("Go live failed: Checklist save failed.", service.GetState().ErrorMessage);
    }

    [Fact]
    public async Task WhenGoLiveTimesOut_ThenErrorStateIsSet()
    {
        using OperatorStateService operatorStateService = new();
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        StubChecklistService checklistService = new(allRequiredChecked: true);
        RecordingChecklistSessionService checklistSessionService = new(delayOnSave: TimeSpan.FromMilliseconds(80));
        GoLiveActionService service = CreateService(
            operatorStateService,
            checklistService,
            checklistSessionService,
            timeout: TimeSpan.FromMilliseconds(25));

        await service.Execute();

        Assert.Equal(OperatorMode.PreLive, operatorStateService.Mode);
        Assert.False(checklistService.HandleGoLiveSucceededCalled);
        Assert.Contains("Go live timed out", service.GetState().ErrorMessage, StringComparison.Ordinal);
    }

    private static GoLiveActionService CreateService(
        IOperatorStateService operatorStateService,
        IPreLiveChecklistService checklistService,
        IChecklistSessionService checklistSessionService,
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null)
    {
        return timeout is null
            ? new GoLiveActionService(
                operatorStateService,
                checklistService,
                checklistSessionService,
                timeProvider ?? TimeProvider.System,
                NullLogger<GoLiveActionService>.Instance)
            : new GoLiveActionService(
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
        private readonly bool _failOnSave;
        private readonly TimeSpan? _delayOnSave;

        public RecordingChecklistSessionService(bool failOnSave = false, TimeSpan? delayOnSave = null)
        {
            _failOnSave = failOnSave;
            _delayOnSave = delayOnSave;
        }

        public List<string> Events { get; } = [];

        public Guid SessionId { get; private set; }

        public async Task Save(Guid sessionId, IPreLiveChecklistService checklist, CancellationToken cancellationToken = default)
        {
            _ = checklist;
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add("checklist.save");

            if (_delayOnSave is not null)
            {
                await Task.Delay(_delayOnSave.Value, cancellationToken);
            }

            if (_failOnSave)
            {
                throw new InvalidOperationException("Checklist save failed.");
            }

            SessionId = sessionId;
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
