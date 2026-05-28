using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Data.Tests;

public sealed class ChecklistSessionServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenSavingChecklistSnapshot_ThenAllChecklistItemsArePersistedForTheSession()
    {
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset recordedAt = new(2026, 6, 1, 18, 30, 0, TimeSpan.Zero);
        ChecklistSessionService service = new(
            DbContextFactory,
            new FixedTimeProvider(recordedAt),
            NullLogger<ChecklistSessionService>.Instance);
        StubChecklistSessionService checklist = new(
            new PreLiveChecklistState
            {
                Items =
                [
                    CreateItem("platform-connection.twitch", "Platform Connections", "Twitch connected", ChecklistItemType.Auto, true, isChecked: true),
                    CreateItem("personal.water-ready", "Personal Prep", "Water ready", ChecklistItemType.Manual, false, isChecked: false),
                    CreateItem("recording-disk-space", "Recording", "Sufficient disk space (≥ 10 GB free)", ChecklistItemType.AutoWithWarn, false, isChecked: false, isWarning: true, warningMessage: "Only 4.5 GB free on recording drive"),
                    CreateItem("ingest-url-copied", "OBS & Technical", "RTMP ingest URL configured in OBS", ChecklistItemType.Manual, true, isChecked: false),
                    CreateItem("platform-connection.youtube", "Platform Connections", "YouTube connected", ChecklistItemType.Auto, true, isChecked: false, isBlocked: true)
                ]
            });

        await service.Save(sessionId, checklist);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        ChecklistSession persistedSession = await dbContext.ChecklistSessions
            .Include(session => session.Items)
            .SingleAsync();

        Assert.Equal(sessionId, persistedSession.SessionId);
        Assert.Equal(recordedAt, persistedSession.RecordedAt);
        Assert.Equal(5, persistedSession.Items.Count);
        Assert.Contains(persistedSession.Items, item => item.ItemId == "platform-connection.twitch" && item.Status == "Checked" && item.WarningMessage is null);
        Assert.Contains(persistedSession.Items, item => item.ItemId == "personal.water-ready" && item.Status == "Warned" && item.WarningMessage is not null);
        Assert.Contains(persistedSession.Items, item => item.ItemId == "recording-disk-space" && item.Status == "Warned" && item.WarningMessage == "Only 4.5 GB free on recording drive");
        Assert.Contains(persistedSession.Items, item => item.ItemId == "ingest-url-copied" && item.Status == "Unchecked" && item.WarningMessage is null);
        Assert.Contains(persistedSession.Items, item => item.ItemId == "platform-connection.youtube" && item.Status == "Blocked" && item.WarningMessage is not null);
    }

    private static ChecklistItemState CreateItem(
        string itemId,
        string category,
        string label,
        ChecklistItemType itemType,
        bool isRequired,
        bool isChecked,
        bool isBlocked = false,
        bool isWarning = false,
        string? warningMessage = null)
    {
        return new ChecklistItemState
        {
            Definition = new ChecklistItemDefinition
            {
                Id = itemId,
                Category = category,
                Label = label,
                Type = itemType,
                IsRequired = isRequired,
                SortOrder = 0
            },
            IsChecked = isChecked,
            IsBlocked = isBlocked,
            IsWarning = isWarning,
            WarningMessage = warningMessage
        };
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

    private sealed class StubChecklistSessionService : IPreLiveChecklistService
    {
        private readonly PreLiveChecklistState _state;

        public StubChecklistSessionService(PreLiveChecklistState state)
        {
            _state = state;
        }

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

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
}
