using System.Diagnostics;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Tests;

public sealed class PreLiveChecklistServiceTests
{
    [Fact]
    public void WhenStreamInfoIsUpdated_ThenAutoChecklistItemsUpdateAndCountsDrop()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);

        PreLiveChecklistState initialState = checklistService.GetState();

        operatorStateService.SetStreamInfo("Pre-live title", "Gaming", ["backend"]);

        PreLiveChecklistState updatedState = checklistService.GetState();

        Assert.Equal(7, initialState.OptionalUncheckedCount);
        Assert.Equal(initialState.RequiredUncheckedCount - 2, updatedState.RequiredUncheckedCount);
        Assert.True(GetItem(updatedState, "stream-info.title").IsChecked);
        Assert.True(GetItem(updatedState, "stream-info.category").IsChecked);
    }

    [Fact]
    public void WhenPlatformConnectionChanges_ThenChecklistRaisesStateChanged()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Disconnected);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);
        int stateChangedCount = 0;

        checklistService.StateChanged += (_, _) => stateChangedCount++;

        twitchService.SetState(PlatformConnectionState.Connected);

        Assert.True(stateChangedCount > 0);
        Assert.True(GetItem(checklistService.GetState(), "platform-connection.twitch").IsChecked);
    }

    [Fact]
    public void WhenPlatformDisconnects_ThenChecklistItemTransitionsToPending()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);

        Assert.True(GetItem(checklistService.GetState(), "platform-connection.twitch").IsChecked);

        twitchService.SetState(PlatformConnectionState.Disconnected);

        ChecklistItemState item = GetItem(checklistService.GetState(), "platform-connection.twitch");

        Assert.False(item.IsChecked);
        Assert.False(item.IsBlocked);
        Assert.Null(item.WarningMessage);
    }

    [Fact]
    public void WhenPlatformEntersErrorState_ThenChecklistItemTransitionsToFailedAndShowsLastError()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Disconnected);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);

        twitchService.SetState(PlatformConnectionState.Error, "Auth token expired");

        ChecklistItemState item = GetItem(checklistService.GetState(), "platform-connection.twitch");

        Assert.False(item.IsChecked);
        Assert.True(item.IsBlocked);
        Assert.True(item.IsWarning);
        Assert.Equal("Auth token expired", item.WarningMessage);
    }

    [Fact]
    public void WhenPlatformIsPendingApproval_ThenNoChecklistItemCreated()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.PendingApproval);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);

        AssertNoItem(checklistService.GetState(), "platform-connection.twitch");
    }

    [Fact]
    public void WhenPlatformIsDisabled_ThenNoChecklistItemCreated()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Disabled);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);

        AssertNoItem(checklistService.GetState(), "platform-connection.twitch");
    }

    [Fact]
    public void WhenAllRequiredItemsAreSatisfied_ThenGoLiveSuccessTransitionsOperatorStateToLive()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateServiceWithoutOverlays(operatorStateService, new FakeRecordingStorageProbe(), twitchService);

        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        operatorStateService.SetManualReminderReviewed("Twitch", "Visibility", true);

        checklistService.SetItemChecked("obs-scene-ready", true);
        checklistService.SetItemChecked("ingest-url-copied", true);
        checklistService.SetItemChecked("audio-levels-set", true);
        Assert.True(checklistService.AllRequiredChecked);

        checklistService.HandleGoLiveSucceeded(new DateTimeOffset(2026, 5, 31, 18, 0, 0, TimeSpan.Zero));

        OperatorStreamState? activeStreamState = operatorStateService.GetActiveStreamState();

        Assert.Equal(OperatorMode.Live, operatorStateService.Mode);
        Assert.NotNull(activeStreamState);
        Assert.Equal("Ship it", activeStreamState.Title);
        Assert.Equal("Science & Technology", activeStreamState.Category);
        Assert.Equal(["services"], activeStreamState.Tags);
    }

    [Fact]
    public void WhenStreamTitleIsCleared_ThenTitleChecklistItemTransitionsToPending()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);

        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", []);
        Assert.True(GetItem(checklistService.GetState(), "stream-info.title").IsChecked);

        operatorStateService.SetStreamInfo(string.Empty, "Science & Technology", []);

        Assert.False(GetItem(checklistService.GetState(), "stream-info.title").IsChecked);
    }

    [Fact]
    public void WhenCategoryIsSet_ThenCategoryChecklistItemTransitionsToChecked()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);

        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", []);

        Assert.True(GetItem(checklistService.GetState(), "stream-info.category").IsChecked);
    }

    [Fact]
    public void WhenRecordingStorageNeedsAttention_ThenRecordingItemsWarnWithoutBlockingGoLive()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        FakeRecordingStorageProbe recordingStorageProbe = new(
            new RecordingStorageStatus(
                false,
                "Recording output folder is unavailable: Access denied",
                false,
                "Only 4.5 GB free on recording drive"));
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, recordingStorageProbe, twitchService);

        PreLiveChecklistState state = checklistService.GetState();
        ChecklistItemState recordingPathItem = GetItem(state, "recording-path-configured");
        ChecklistItemState diskSpaceItem = GetItem(state, "recording-disk-space");

        Assert.False(recordingPathItem.Definition.IsRequired);
        Assert.False(recordingPathItem.IsChecked);
        Assert.True(recordingPathItem.IsWarning);
        Assert.Equal("Recording output folder is unavailable: Access denied", recordingPathItem.WarningMessage);
        Assert.False(diskSpaceItem.Definition.IsRequired);
        Assert.False(diskSpaceItem.IsChecked);
        Assert.True(diskSpaceItem.IsWarning);
        Assert.Equal("Only 4.5 GB free on recording drive", diskSpaceItem.WarningMessage);
        Assert.False(state.AllRequiredChecked);
    }

    [Fact]
    public void WhenRecordingDiskSpaceIsHealthy_ThenRecordingDiskItemIsChecked()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(
            operatorStateService,
            new FakeRecordingStorageProbe(new RecordingStorageStatus(true, null, true, null)),
            twitchService);

        ChecklistItemState item = GetItem(checklistService.GetState(), "recording-disk-space");

        Assert.True(item.IsChecked);
        Assert.True(item.IsAutoChecked);
        Assert.False(item.IsWarning);
    }

    [Fact]
    public void WhenRecordingPathExists_ThenPathChecklistItemIsChecked()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(
            operatorStateService,
            new FakeRecordingStorageProbe(new RecordingStorageStatus(true, null, false, "Only 4.5 GB free on recording drive")),
            twitchService);

        ChecklistItemState item = GetItem(checklistService.GetState(), "recording-path-configured");

        Assert.True(item.IsChecked);
        Assert.True(item.IsAutoChecked);
        Assert.False(item.IsWarning);
    }

    [Fact]
    public void WhenChecklistIsReset_ThenManualItemsAndReminderReviewsAreCleared()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);

        operatorStateService.SetManualReminderReviewed("Twitch", "Visibility", true);
        checklistService.SetItemChecked("ingest-url-copied", true);
        checklistService.Reset();

        PreLiveChecklistState resetState = checklistService.GetState();

        Assert.False(operatorStateService.IsManualReminderReviewed("Twitch", "Visibility"));
        Assert.False(GetItem(resetState, "ingest-url-copied").IsChecked);
        Assert.False(GetItem(resetState, "stream-info.manual-reminders").IsChecked);
    }

    [Fact]
    public async Task WhenOverlayVerificationActionRuns_ThenOnlyTestableItemsRenderAndAutoCheckAfterFlash()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(
            operatorStateService,
            new FakeRecordingStorageProbe(),
            TimeSpan.FromSeconds(30),
            new StreamingOptions(),
            TimeSpan.FromMilliseconds(10),
            twitchService);
        string? triggeredComponentName = null;

        operatorStateService.OverlayTestTriggered += (_, componentName) => triggeredComponentName = componentName;

        PreLiveChecklistState initialState = checklistService.GetState();
        ChecklistItemState overlayItem = GetItem(initialState, "overlay.chat-feed");

        Assert.Equal("Overlay Verification", overlayItem.Definition.Category);
        Assert.Equal("Chat Feed visible", overlayItem.Definition.Label);
        Assert.Equal(ChecklistItemType.Action, overlayItem.Definition.Type);
        Assert.False(overlayItem.Definition.IsRequired);
        Assert.Equal("Test", overlayItem.Definition.ActionLabel);
        Assert.DoesNotContain(initialState.Items, item => item.Definition.Id == "overlay.static-card");

        await checklistService.TriggerAction("overlay.chat-feed");

        PreLiveChecklistState updatedState = checklistService.GetState();

        Assert.Equal("Chat Feed", triggeredComponentName);
        Assert.True(GetItem(updatedState, "overlay.chat-feed").IsChecked);
    }

    [Fact]
    public void WhenNoTestableOverlayRegistrationsExist_ThenOverlayVerificationCategoryIsEmpty()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = new(
            operatorStateService,
            [twitchService],
            new FakeOverlayService(new FakeOverlayComponent("Static Card")),
            new FakePlatformManualReminderProvider(),
            new FakeRecordingStorageProbe(),
            Options.Create(new StreamingOptions()),
            TimeProvider.System,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(10));

        PreLiveChecklistState state = checklistService.GetState();

        Assert.DoesNotContain(state.Items, item => item.Definition.Category == "Overlay Verification");
    }

    [Fact]
    public void WhenBuildingTechnicalCategory_ThenObsItemsMatchIssueDefinitionAndExposeIngestUrl()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        StreamingOptions streamingOptions = new()
        {
            IngestUrl = "rtmp://localhost:1935/live"
        };
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, new FakeRecordingStorageProbe(), TimeSpan.FromSeconds(30), streamingOptions, twitchService);

        PreLiveChecklistState state = checklistService.GetState();

        ChecklistItemState obsSceneReady = GetItem(state, "obs-scene-ready");
        ChecklistItemState ingestUrlCopied = GetItem(state, "ingest-url-copied");
        ChecklistItemState audioLevelsSet = GetItem(state, "audio-levels-set");
        ChecklistItemState testStreamDone = GetItem(state, "test-stream-done");

        Assert.Equal("OBS & Technical", obsSceneReady.Definition.Category);
        Assert.True(obsSceneReady.Definition.IsRequired);
        Assert.Equal("OBS scene configured and active", obsSceneReady.Definition.Label);
        Assert.True(ingestUrlCopied.Definition.IsRequired);
        Assert.Equal("RTMP ingest URL configured in OBS", ingestUrlCopied.Definition.Label);
        Assert.Equal(streamingOptions.IngestUrl, ingestUrlCopied.Definition.InlineValue);
        Assert.True(ingestUrlCopied.Definition.CanCopyInlineValue);
        Assert.True(audioLevelsSet.Definition.IsRequired);
        Assert.Equal("Audio levels checked", audioLevelsSet.Definition.Label);
        Assert.False(testStreamDone.Definition.IsRequired);
        Assert.Equal("Test stream completed", testStreamDone.Definition.Label);
    }

    [Fact]
    public void WhenIngestUrlItemIsChecked_ThenChecklistRaisesStateChanged()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateService(operatorStateService, twitchService);
        int stateChangedCount = 0;

        checklistService.StateChanged += (_, _) => stateChangedCount++;

        checklistService.SetItemChecked("ingest-url-copied", true);

        Assert.Equal(1, stateChangedCount);
        Assert.True(GetItem(checklistService.GetState(), "ingest-url-copied").IsChecked);
    }

    [Fact]
    public async Task WhenRecordingStorageChangesDuringPolling_ThenChecklistRaisesStateChangedAndStopsCleanly()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        FakeRecordingStorageProbe recordingStorageProbe = new(
            new RecordingStorageStatus(
                true,
                null,
                true,
                null));
        using PreLiveChecklistService checklistService = CreateService(
            operatorStateService,
            recordingStorageProbe,
            TimeSpan.FromMilliseconds(25),
            twitchService);
        int stateChangedCount = 0;

        checklistService.StateChanged += (_, _) => Interlocked.Increment(ref stateChangedCount);

        await checklistService.StartAsync(CancellationToken.None);

        recordingStorageProbe.Status = new RecordingStorageStatus(
            true,
            null,
            false,
            "Only 6.2 GB free on recording drive");

        bool changed = await WaitForConditionAsync(() => Volatile.Read(ref stateChangedCount) > 0, TimeSpan.FromSeconds(2));
        Assert.True(changed);

        int countBeforeStop = Volatile.Read(ref stateChangedCount);
        await checklistService.StopAsync(CancellationToken.None);

        recordingStorageProbe.Status = new RecordingStorageStatus(
            true,
            null,
            true,
            null);

        await Task.Delay(120);

        Assert.Equal(countBeforeStop, Volatile.Read(ref stateChangedCount));
    }

    [Fact]
    public async Task WhenServiceStarts_ThenPersonalPrepItemsLoadFromCatalog()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        FakeCustomChecklistItemCatalog customChecklistItemCatalog = new(
        [
            new CustomChecklistItemDefinition
            {
                Id = 41,
                Label = "Stretch wrists",
                DisplayOrder = 2,
                IsEnabled = true
            },
            new CustomChecklistItemDefinition
            {
                Id = 40,
                Label = "Hydrate",
                DisplayOrder = 1,
                IsEnabled = true
            },
            new CustomChecklistItemDefinition
            {
                Id = 42,
                Label = "Muted item",
                DisplayOrder = 3,
                IsEnabled = false
            }
        ]);
        using PreLiveChecklistService checklistService = new(
            operatorStateService,
            [twitchService],
            new FakeOverlayService(),
            new FakePlatformManualReminderProvider(),
            new FakeRecordingStorageProbe(),
            customChecklistItemCatalog,
            Options.Create(new StreamingOptions()),
            TimeProvider.System,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(10));

        await checklistService.StartAsync(CancellationToken.None);
        PreLiveChecklistState state = checklistService.GetState();

        ChecklistItemState[] personalPrepItems = state.Items
            .Where(item => item.Definition.Category == "Personal Prep")
            .ToArray();

        Assert.Equal(
            ["Hydrate", "Stretch wrists"],
            personalPrepItems.Select(item => item.Definition.Label).ToArray());
        Assert.All(personalPrepItems, item => Assert.False(item.Definition.IsRequired));
        Assert.DoesNotContain(state.Items, item => item.Definition.Id == "personal-prep-42");

        await checklistService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenPersonalPrepCatalogReloads_ThenItemsResetToPendingAndRaiseStateChanged()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        FakeCustomChecklistItemCatalog customChecklistItemCatalog = new(
        [
            new CustomChecklistItemDefinition
            {
                Id = 51,
                Label = "Check mic arm",
                DisplayOrder = 1,
                IsEnabled = true
            }
        ]);
        using PreLiveChecklistService checklistService = new(
            operatorStateService,
            [twitchService],
            new FakeOverlayService(),
            new FakePlatformManualReminderProvider(),
            new FakeRecordingStorageProbe(),
            customChecklistItemCatalog,
            Options.Create(new StreamingOptions()),
            TimeProvider.System);
        int stateChangedCount = 0;

        checklistService.StateChanged += (_, _) => stateChangedCount++;

        await checklistService.Reload(CancellationToken.None);
        checklistService.SetItemChecked("personal-prep-51", true);
        customChecklistItemCatalog.SetItems(
        [
            new CustomChecklistItemDefinition
            {
                Id = 52,
                Label = "Post socials",
                DisplayOrder = 1,
                IsEnabled = true
            }
        ]);

        await checklistService.Reload(CancellationToken.None);
        PreLiveChecklistState state = checklistService.GetState();

        Assert.DoesNotContain(state.Items, item => item.Definition.Id == "personal-prep-51");
        ChecklistItemState reloadedItem = GetItem(state, "personal-prep-52");
        Assert.False(reloadedItem.IsChecked);
        Assert.True(stateChangedCount >= 3);
    }

    [Fact]
    public void WhenOptionalItemsRemainUnchecked_ThenAllRequiredCheckedStaysTrue()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Connected);
        using PreLiveChecklistService checklistService = CreateServiceWithoutOverlays(
            operatorStateService,
            new FakeRecordingStorageProbe(new RecordingStorageStatus(false, "Set a recording output folder to enable local capture.", false, "Only 4.5 GB free on recording drive")),
            twitchService);

        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        operatorStateService.SetManualReminderReviewed("Twitch", "Visibility", true);
        checklistService.SetItemChecked("obs-scene-ready", true);
        checklistService.SetItemChecked("ingest-url-copied", true);
        checklistService.SetItemChecked("audio-levels-set", true);

        PreLiveChecklistState state = checklistService.GetState();

        Assert.True(state.AllRequiredChecked);
        Assert.False(GetItem(state, "recording-path-configured").IsChecked);
        Assert.False(GetItem(state, "recording-disk-space").IsChecked);
    }

    [Fact]
    public void WhenRequiredPlatformErrors_ThenAllRequiredCheckedIsFalse()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new(PlatformConnectionState.Error);
        using PreLiveChecklistService checklistService = CreateServiceWithoutOverlays(operatorStateService, new FakeRecordingStorageProbe(), twitchService);

        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["services"]);
        operatorStateService.SetManualReminderReviewed("Twitch", "Visibility", true);
        checklistService.SetItemChecked("obs-scene-ready", true);
        checklistService.SetItemChecked("ingest-url-copied", true);
        checklistService.SetItemChecked("audio-levels-set", true);

        Assert.False(checklistService.AllRequiredChecked);
    }

    private static ChecklistItemState GetItem(PreLiveChecklistState state, string itemId)
    {
        return Assert.Single(state.Items, item => item.Definition.Id == itemId);
    }

    private static void AssertNoItem(PreLiveChecklistState state, string itemId)
    {
        Assert.DoesNotContain(state.Items, item => item.Definition.Id == itemId);
    }

    private static PreLiveChecklistService CreateService(
        OperatorStateService operatorStateService,
        params IPlatformConnection[] platformConnections)
    {
        return CreateService(operatorStateService, new FakeRecordingStorageProbe(), TimeSpan.FromSeconds(30), new StreamingOptions(), TimeSpan.FromMilliseconds(3200), platformConnections);
    }

    private static PreLiveChecklistService CreateService(
        OperatorStateService operatorStateService,
        FakeRecordingStorageProbe recordingStorageProbe,
        TimeSpan recordingPollInterval,
        params IPlatformConnection[] platformConnections)
    {
        return CreateService(operatorStateService, recordingStorageProbe, recordingPollInterval, new StreamingOptions(), TimeSpan.FromMilliseconds(3200), platformConnections);
    }

    private static PreLiveChecklistService CreateService(
        OperatorStateService operatorStateService,
        FakeRecordingStorageProbe recordingStorageProbe,
        params IPlatformConnection[] platformConnections)
    {
        return CreateService(operatorStateService, recordingStorageProbe, TimeSpan.FromSeconds(30), new StreamingOptions(), TimeSpan.FromMilliseconds(3200), platformConnections);
    }

    private static PreLiveChecklistService CreateService(
        OperatorStateService operatorStateService,
        FakeRecordingStorageProbe recordingStorageProbe,
        TimeSpan recordingPollInterval,
        StreamingOptions streamingOptions,
        params IPlatformConnection[] platformConnections)
    {
        return CreateService(
            operatorStateService,
            recordingStorageProbe,
            recordingPollInterval,
            streamingOptions,
            TimeSpan.FromMilliseconds(3200),
            platformConnections);
    }

    private static PreLiveChecklistService CreateServiceWithoutOverlays(
        OperatorStateService operatorStateService,
        FakeRecordingStorageProbe recordingStorageProbe,
        params IPlatformConnection[] platformConnections)
    {
        return new PreLiveChecklistService(
            operatorStateService,
            platformConnections,
            new FakeOverlayService(),
            new FakePlatformManualReminderProvider(),
            recordingStorageProbe,
            Options.Create(new StreamingOptions()),
            TimeProvider.System);
    }

    private static PreLiveChecklistService CreateService(
        OperatorStateService operatorStateService,
        FakeRecordingStorageProbe recordingStorageProbe,
        TimeSpan recordingPollInterval,
        StreamingOptions streamingOptions,
        TimeSpan overlayActionCompletionDelay,
        params IPlatformConnection[] platformConnections)
    {
        return new PreLiveChecklistService(
            operatorStateService,
            platformConnections,
            new FakeOverlayService(new FakeTestableOverlayComponent("Chat Feed"), new FakeOverlayComponent("Static Card")),
            new FakePlatformManualReminderProvider(),
            recordingStorageProbe,
            Options.Create(streamingOptions),
            TimeProvider.System,
            recordingPollInterval,
            overlayActionCompletionDelay);
    }

    private static async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return condition();
    }

    private sealed class FakeOverlayService : IOverlayService
    {
        private readonly List<IOverlayComponent> _components;

        public FakeOverlayService(params IOverlayComponent[] components)
        {
            _components = [.. components];
        }

        public void Register(IOverlayComponent component)
        {
            _components.Add(component);
        }

        public void Unregister(IOverlayComponent component)
        {
            _components.Remove(component);
        }

        public IReadOnlyList<IOverlayComponent> GetComponents()
        {
            return [.. _components];
        }
    }

    private sealed class FakeOverlayComponent : IOverlayComponent
    {
        public FakeOverlayComponent(string componentName)
        {
            ComponentName = componentName;
        }

        public string ComponentName { get; }

        public Type ComponentType => typeof(object);
    }

    private sealed class FakeCustomChecklistItemCatalog : ICustomChecklistItemCatalog
    {
        private IReadOnlyList<CustomChecklistItemDefinition> _items;

        public FakeCustomChecklistItemCatalog(IReadOnlyList<CustomChecklistItemDefinition> items)
        {
            _items = items;
        }

        public Task<IReadOnlyList<CustomChecklistItemDefinition>> List(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(_items);
        }

        public void SetItems(IReadOnlyList<CustomChecklistItemDefinition> items)
        {
            _items = items;
        }
    }

    private sealed class FakeTestableOverlayComponent : ITestableOverlayComponent
    {
        public FakeTestableOverlayComponent(string componentName)
        {
            ComponentName = componentName;
        }

        public string ComponentName { get; }

        public Type ComponentType => typeof(object);

        public Task TriggerTestFlash(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlatformManualReminderProvider : IPlatformManualReminderProvider
    {
        public IReadOnlyList<PlatformManualReminder> GetReminders()
        {
            return
            [
                new PlatformManualReminder
                {
                    Platform = "Twitch",
                    Setting = "Visibility",
                    ReminderText = "Check visibility before going live."
                }
            ];
        }
    }

    private sealed class FakeRecordingStorageProbe : IRecordingStorageProbe
    {
        public FakeRecordingStorageProbe()
            : this(new RecordingStorageStatus(false, "Set a recording output folder to enable local capture.", false, "Recording drive monitoring starts after a recording folder is configured."))
        {
        }

        public FakeRecordingStorageProbe(RecordingStorageStatus status)
        {
            Status = status;
        }

        public RecordingStorageStatus Status { get; set; }

        public RecordingStorageStatus GetStatus()
        {
            return Status;
        }
    }

    private sealed class FakeTwitchService : ITwitchService
    {
        public FakeTwitchService(PlatformConnectionState state)
        {
            State = state;
        }

        public string PlatformName => "Twitch";

        public TwitchConnectionState ConnectionState => State == PlatformConnectionState.Connected
            ? TwitchConnectionState.Connected
            : TwitchConnectionState.NotAuthorized;

        public bool IsStreamLive => false;

        public TwitchStreamState StreamState => new();

        public PlatformConnectionState State { get; private set; }

        public string? LastError { get; private set; }

        public bool Connected => State == PlatformConnectionState.Connected;

        public event EventHandler<TwitchConnectionState>? ConnectionStateChanged;

        public event EventHandler<bool>? StreamLiveStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<ChatEvent>? OnChatMessageReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived
        {
            add { }
            remove { }
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RefreshStreamState(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Connect(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void SetState(PlatformConnectionState state, string? lastError = null)
        {
            State = state;
            LastError = lastError;
            ConnectionStateChanged?.Invoke(this, ConnectionState);
        }
    }
}
