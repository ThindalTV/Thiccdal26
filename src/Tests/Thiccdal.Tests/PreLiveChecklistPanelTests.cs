using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Modules.Control.Components.PreLive;

namespace Thiccdal.Tests;

public sealed class PreLiveChecklistPanelTests
{
    [Fact]
    public void WhenOperatorIsPreLiveWithoutEnabledPersonalPrepItems_ThenPanelShowsManageEntryPoint()
    {
        using PreLiveChecklistPanelTestHarness harness = new(OperatorMode.PreLive);

        IRenderedComponent<PreLiveChecklistPanel> cut = harness.Render();

        Assert.Contains("Manage personal prep items", cut.Markup);
        Assert.Contains("No personal prep items are enabled yet", cut.Markup);
    }

    [Fact]
    public void WhenOperatorIsLive_ThenPersonalPrepManageButtonIsHidden()
    {
        using PreLiveChecklistPanelTestHarness harness = new(OperatorMode.Live);

        IRenderedComponent<PreLiveChecklistPanel> cut = harness.Render();

        Assert.DoesNotContain("Manage personal prep items", cut.Markup);
    }

    private sealed class PreLiveChecklistPanelTestHarness : IDisposable
    {
        private readonly TestContext _context = new();

        public PreLiveChecklistPanelTestHarness(OperatorMode mode)
        {
            OperatorStateService = new FakeOperatorStateService(mode);
            ChecklistService = new FakePreLiveChecklistService();
            _context.Services.AddSingleton<IOperatorStateService>(OperatorStateService);
            _context.Services.AddSingleton<IPreLiveChecklistService>(ChecklistService);
            _context.Services.AddSingleton<IOverlayService>(new FakeOverlayService());
            _context.Services.AddSingleton<ICustomChecklistItemManagementService>(new FakeCustomChecklistItemManagementService());
        }

        public FakeOperatorStateService OperatorStateService { get; }

        public FakePreLiveChecklistService ChecklistService { get; }

        public IRenderedComponent<PreLiveChecklistPanel> Render()
        {
            return _context.RenderComponent<PreLiveChecklistPanel>();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }

    private sealed class FakePreLiveChecklistService : IPreLiveChecklistService
    {
        private readonly PreLiveChecklistState _state = new()
        {
            Items =
            [
                new ChecklistItemState
                {
                    Definition = new ChecklistItemDefinition
                    {
                        Id = "stream-info.title",
                        Category = "Stream Info",
                        Label = "Stream title set",
                        Type = ChecklistItemType.Auto,
                        IsRequired = true,
                        SortOrder = 1
                    }
                }
            ],
            CompletedCount = 0,
            TotalCount = 1,
            RequiredUncheckedCount = 1,
            OptionalUncheckedCount = 0,
            AllRequiredChecked = false
        };

        public event EventHandler? StateChanged;

        public bool AllRequiredChecked => _state.AllRequiredChecked;

        public int RequiredUncheckedCount => _state.RequiredUncheckedCount;

        public int OptionalUncheckedCount => _state.OptionalUncheckedCount;

        public PreLiveChecklistState GetState()
        {
            return _state;
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
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public void Reset()
        {
        }

        public void HandleGoLiveSucceeded(DateTimeOffset? startedAt = null, Guid? sessionId = null)
        {
            _ = startedAt;
            _ = sessionId;
        }
    }

    private sealed class FakeOperatorStateService : IOperatorStateService
    {
        public FakeOperatorStateService(OperatorMode mode)
        {
            Mode = mode;
        }

        public event EventHandler? StateChanged;

        public event EventHandler<string>? OverlayTestTriggered;

        public OperatorMode Mode { get; private set; }

        public string StreamTitle => string.Empty;

        public string StreamCategory => string.Empty;

        public IReadOnlyList<string> StreamTags => [];

        public DateTimeOffset? LiveStartedAt => null;

        public int TeleprompterScrollPosition => 0;

        public IReadOnlyList<Thiccdal.Infrastructure.Questions.QueuedQuestion> QuestionQueue => [];

        public Thiccdal.Infrastructure.Questions.QuestionDashboardState GetQuestionState()
        {
            throw new NotSupportedException();
        }

        public OperatorStreamState? GetActiveStreamState()
        {
            return null;
        }

        public void TriggerOverlayTest(string componentName)
        {
            OverlayTestTriggered?.Invoke(this, componentName);
        }

        public void ScrollTeleprompter(Thiccdal.Infrastructure.Teleprompter.ScrollDirection direction)
        {
            _ = direction;
        }

        public void AddQuestion(Thiccdal.Infrastructure.Questions.QueuedQuestion question)
        {
            _ = question;
        }

        public void DismissQuestion(Guid questionId)
        {
            _ = questionId;
        }

        public void FeatureQuestion(Guid questionId)
        {
            _ = questionId;
        }

        public void CompleteQuestion(Guid questionId)
        {
            _ = questionId;
        }

        public void SetMode(OperatorMode mode)
        {
            Mode = mode;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetStreamInfo(string title, string category, IReadOnlyList<string> tags)
        {
            _ = title;
            _ = category;
            _ = tags;
        }

        public void BeginLiveSession(DateTimeOffset? startedAt = null, Guid? sessionId = null)
        {
            _ = startedAt;
            _ = sessionId;
        }

        public bool IsManualReminderReviewed(string platform, string setting)
        {
            _ = platform;
            _ = setting;
            return false;
        }

        public void SetManualReminderReviewed(string platform, string setting, bool isReviewed)
        {
            _ = platform;
            _ = setting;
            _ = isReviewed;
        }

        public bool ClearManualReminderReviews()
        {
            return false;
        }

        public void SetActiveStreamState(OperatorStreamState? streamState)
        {
            _ = streamState;
        }

        public bool AreAllManualRemindersReviewed(IEnumerable<PlatformManualReminder> reminders)
        {
            _ = reminders;
            return false;
        }
    }

    private sealed class FakeOverlayService : IOverlayService
    {
        public void Register(IOverlayComponent component)
        {
            _ = component;
        }

        public void Unregister(IOverlayComponent component)
        {
            _ = component;
        }

        public IReadOnlyList<IOverlayComponent> GetComponents()
        {
            return [];
        }
    }

    private sealed class FakeCustomChecklistItemManagementService : ICustomChecklistItemManagementService
    {
        public Task<IReadOnlyList<CustomChecklistItemDefinition>> List(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<CustomChecklistItemDefinition>>([]);
        }

        public Task<CustomChecklistItemDefinition> Create(string label, CancellationToken cancellationToken = default)
        {
            _ = label;
            _ = cancellationToken;
            throw new NotSupportedException();
        }

        public Task<CustomChecklistItemDefinition?> Update(CustomChecklistItemDefinition item, CancellationToken cancellationToken = default)
        {
            _ = item;
            _ = cancellationToken;
            throw new NotSupportedException();
        }

        public Task<bool> Delete(int id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;
            throw new NotSupportedException();
        }

        public Task<bool> MoveUp(int id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;
            throw new NotSupportedException();
        }

        public Task<bool> MoveDown(int id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;
            throw new NotSupportedException();
        }
    }
}
