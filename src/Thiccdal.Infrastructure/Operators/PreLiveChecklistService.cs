using Thiccdal.Infrastructure.Discord;
using Thiccdal.Infrastructure.Facebook;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Infrastructure.X;
using Thiccdal.Infrastructure.YouTube;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Builds the operator-facing pre-live checklist from runtime state and operator input.
/// </summary>
public sealed class PreLiveChecklistService : IPreLiveChecklistService, IHostedService, IDisposable
{
    private static readonly TimeSpan DefaultOverlayActionCompletionDelay = TimeSpan.FromMilliseconds(3200);
    private const string PlatformConnectionsCategory = "Platform Connections";
    private const string StreamInfoCategory = "Stream Info";
    private const string ObsTechnicalCategory = "OBS & Technical";
    private const string RecordingCategory = "Recording";
    private const string OverlayVerificationCategory = "Overlay Verification";
    private const string PersonalPrepCategory = "Personal Prep";

    private readonly IOperatorStateService _operatorStateService;
    private readonly IOverlayService _overlayService;
    private readonly IPlatformManualReminderProvider _manualReminderProvider;
    private readonly IRecordingStorageProbe _recordingStorageProbe;
    private readonly ICustomChecklistItemCatalog _customChecklistItemCatalog;
    private readonly IReadOnlyList<IPlatformConnection> _platformConnections;
    private readonly IOptions<StreamingOptions> _streamingOptions;
    private readonly Lock _recordingStatusLock = new();
    private readonly Lock _checkedItemsLock = new();
    private readonly Lock _personalPrepDefinitionsLock = new();
    private readonly Dictionary<string, DateTimeOffset> _checkedItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Action> _unsubscribeActions = [];
    private List<ChecklistItemDefinition> _personalPrepDefinitions = CreateDefaultPersonalPrepDefinitions();
    private readonly TimeSpan _recordingPollInterval;
    private readonly TimeSpan _overlayActionCompletionDelay;

    private CancellationTokenSource? _recordingPollCts;
    private Task? _recordingPollTask;
    private RecordingStorageStatus _lastObservedRecordingStatus;

    [ActivatorUtilitiesConstructor]
    public PreLiveChecklistService(
        IOperatorStateService operatorStateService,
        IEnumerable<IPlatformConnection> platformConnections,
        IOverlayService overlayService,
        IPlatformManualReminderProvider manualReminderProvider,
        IRecordingStorageProbe recordingStorageProbe,
        IOptions<StreamingOptions> streamingOptions,
        TimeProvider timeProvider)
        : this(
            operatorStateService,
            platformConnections,
            overlayService,
            manualReminderProvider,
            recordingStorageProbe,
            new StaticCustomChecklistItemCatalog(CreateDefaultCatalogItems()),
            streamingOptions,
            timeProvider,
            TimeSpan.FromSeconds(30),
            DefaultOverlayActionCompletionDelay)
    {
    }

    public PreLiveChecklistService(
        IOperatorStateService operatorStateService,
        IEnumerable<IPlatformConnection> platformConnections,
        IOverlayService overlayService,
        IPlatformManualReminderProvider manualReminderProvider,
        IRecordingStorageProbe recordingStorageProbe,
        IOptions<StreamingOptions> streamingOptions,
        TimeProvider timeProvider,
        TimeSpan recordingPollInterval)
        : this(
            operatorStateService,
            platformConnections,
            overlayService,
            manualReminderProvider,
            recordingStorageProbe,
            new StaticCustomChecklistItemCatalog(CreateDefaultCatalogItems()),
            streamingOptions,
            timeProvider,
            recordingPollInterval,
            DefaultOverlayActionCompletionDelay)
    {
    }

    public PreLiveChecklistService(
        IOperatorStateService operatorStateService,
        IEnumerable<IPlatformConnection> platformConnections,
        IOverlayService overlayService,
        IPlatformManualReminderProvider manualReminderProvider,
        IRecordingStorageProbe recordingStorageProbe,
        ICustomChecklistItemCatalog customChecklistItemCatalog,
        IOptions<StreamingOptions> streamingOptions,
        TimeProvider timeProvider)
        : this(
            operatorStateService,
            platformConnections,
            overlayService,
            manualReminderProvider,
            recordingStorageProbe,
            customChecklistItemCatalog,
            streamingOptions,
            timeProvider,
            TimeSpan.FromSeconds(30),
            DefaultOverlayActionCompletionDelay)
    {
    }

    public PreLiveChecklistService(
        IOperatorStateService operatorStateService,
        IEnumerable<IPlatformConnection> platformConnections,
        IOverlayService overlayService,
        IPlatformManualReminderProvider manualReminderProvider,
        IRecordingStorageProbe recordingStorageProbe,
        ICustomChecklistItemCatalog customChecklistItemCatalog,
        IOptions<StreamingOptions> streamingOptions,
        TimeProvider timeProvider,
        TimeSpan recordingPollInterval)
        : this(
            operatorStateService,
            platformConnections,
            overlayService,
            manualReminderProvider,
            recordingStorageProbe,
            customChecklistItemCatalog,
            streamingOptions,
            timeProvider,
            recordingPollInterval,
            DefaultOverlayActionCompletionDelay)
    {
    }

    public PreLiveChecklistService(
        IOperatorStateService operatorStateService,
        IEnumerable<IPlatformConnection> platformConnections,
        IOverlayService overlayService,
        IPlatformManualReminderProvider manualReminderProvider,
        IRecordingStorageProbe recordingStorageProbe,
        IOptions<StreamingOptions> streamingOptions,
        TimeProvider timeProvider,
        TimeSpan recordingPollInterval,
        TimeSpan overlayActionCompletionDelay)
        : this(
            operatorStateService,
            platformConnections,
            overlayService,
            manualReminderProvider,
            recordingStorageProbe,
            new StaticCustomChecklistItemCatalog(CreateDefaultCatalogItems()),
            streamingOptions,
            timeProvider,
            recordingPollInterval,
            overlayActionCompletionDelay)
    {
    }

    public PreLiveChecklistService(
        IOperatorStateService operatorStateService,
        IEnumerable<IPlatformConnection> platformConnections,
        IOverlayService overlayService,
        IPlatformManualReminderProvider manualReminderProvider,
        IRecordingStorageProbe recordingStorageProbe,
        ICustomChecklistItemCatalog customChecklistItemCatalog,
        IOptions<StreamingOptions> streamingOptions,
        TimeProvider timeProvider,
        TimeSpan recordingPollInterval,
        TimeSpan overlayActionCompletionDelay)
    {
        ArgumentNullException.ThrowIfNull(recordingStorageProbe);
        ArgumentNullException.ThrowIfNull(customChecklistItemCatalog);
        ArgumentNullException.ThrowIfNull(streamingOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(recordingPollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(overlayActionCompletionDelay, TimeSpan.Zero);

        _operatorStateService = operatorStateService;
        _overlayService = overlayService;
        _manualReminderProvider = manualReminderProvider;
        _recordingStorageProbe = recordingStorageProbe;
        _customChecklistItemCatalog = customChecklistItemCatalog;
        _platformConnections = platformConnections.ToArray();
        _streamingOptions = streamingOptions;
        _recordingPollInterval = recordingPollInterval;
        _overlayActionCompletionDelay = overlayActionCompletionDelay;
        _lastObservedRecordingStatus = _recordingStorageProbe.GetStatus();

        _operatorStateService.StateChanged += HandleDependencyStateChanged;
        _unsubscribeActions.Add(() => _operatorStateService.StateChanged -= HandleDependencyStateChanged);

        SubscribeToPlatformEvents(_platformConnections);
    }

    public event EventHandler? StateChanged;

    public bool AllRequiredChecked => GetState().AllRequiredChecked;

    public int RequiredUncheckedCount => GetState().RequiredUncheckedCount;

    public int OptionalUncheckedCount => GetState().OptionalUncheckedCount;

    public PreLiveChecklistState GetState()
    {
        ChecklistItemState[] items = BuildItems();

        return new PreLiveChecklistState
        {
            Items = items,
            RequiredUncheckedCount = items.Count(static item => item.Definition.IsRequired && !item.IsChecked),
            OptionalUncheckedCount = items.Count(static item => !item.Definition.IsRequired && !item.IsChecked),
            CompletedCount = items.Count(static item => item.IsChecked),
            TotalCount = items.Length,
            AllRequiredChecked = items.All(static item => !item.Definition.IsRequired || item.IsChecked)
        };
    }

    public void SetItemChecked(string itemId, bool isChecked)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        bool changed;

        lock (_checkedItemsLock)
        {
            changed = _checkedItems.ContainsKey(itemId) != isChecked;

            if (isChecked)
            {
                _checkedItems[itemId] = DateTimeOffset.UtcNow;
            }
            else
            {
                _checkedItems.Remove(itemId);
            }
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task TriggerAction(string itemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        if (!TryResolveOverlayComponentName(itemId, out string componentName))
        {
            throw new InvalidOperationException($"No action trigger is registered for checklist item '{itemId}'.");
        }

        _operatorStateService.TriggerOverlayTest(componentName ?? string.Empty);
        await Task.Delay(_overlayActionCompletionDelay, cancellationToken);
        SetItemChecked(itemId, true);
    }

    public async Task Reload(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CustomChecklistItemDefinition> customItems = await _customChecklistItemCatalog.List(cancellationToken);
        List<ChecklistItemDefinition> personalPrepDefinitions = BuildPersonalPrepDefinitions(customItems);

        lock (_personalPrepDefinitionsLock)
        {
            _personalPrepDefinitions = personalPrepDefinitions;
        }

        lock (_checkedItemsLock)
        {
            string[] personalPrepItemIds =
            [
                .. _checkedItems.Keys.Where(static itemId => IsPersonalPrepItemId(itemId))
            ];

            foreach (string itemId in personalPrepItemIds)
            {
                _checkedItems.Remove(itemId);
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        bool clearedCheckedItems;

        lock (_checkedItemsLock)
        {
            clearedCheckedItems = _checkedItems.Count > 0;
            _checkedItems.Clear();
        }

        bool clearedReminderReviews = _operatorStateService.ClearManualReminderReviews();
        if (clearedCheckedItems && !clearedReminderReviews)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void HandleGoLiveSucceeded(DateTimeOffset? startedAt = null, Guid? sessionId = null)
    {
        if (!AllRequiredChecked)
        {
            throw new InvalidOperationException("Cannot transition to live mode while required checklist items remain unchecked.");
        }

        _operatorStateService.BeginLiveSession(startedAt, sessionId);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_recordingPollTask is not null)
        {
            return;
        }

        await Reload(cancellationToken);

        _recordingPollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _recordingPollTask = PollRecordingState(_recordingPollCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? recordingPollCts = Interlocked.Exchange(ref _recordingPollCts, null);
        Task? recordingPollTask = Interlocked.Exchange(ref _recordingPollTask, null);

        if (recordingPollCts is null)
        {
            return;
        }

        await recordingPollCts.CancelAsync();

        try
        {
            if (recordingPollTask is not null)
            {
                await recordingPollTask.WaitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            recordingPollCts.Dispose();
        }
    }

    public void Dispose()
    {
        if (_recordingPollCts is not null)
        {
            _recordingPollCts.Cancel();
            _recordingPollCts.Dispose();
            _recordingPollCts = null;
            _recordingPollTask = null;
        }

        foreach (Action unsubscribe in _unsubscribeActions)
        {
            unsubscribe();
        }

        GC.SuppressFinalize(this);
    }

    private static string BuildOverlayItemId(string componentName)
    {
        return $"overlay.{BuildSlug(componentName)}";
    }

    private static string BuildPlatformConnectionItemId(string platformName)
    {
        return $"platform-connection.{BuildSlug(platformName)}";
    }

    private static string BuildSlug(string value)
    {
        return string.Join(
            '-',
            value.Split([' ', '/', '\\', '.', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
    }

    private ChecklistItemState[] BuildItems()
    {
        RecordingStorageStatus recordingStatus = GetRecordingStatus();
        List<ChecklistItemState> items =
        [
            .. BuildPlatformConnectionItems(),
            CreateStreamInfoItem(
                "stream-info.title",
                "Stream title set",
                sortOrder: 100,
                isChecked: !string.IsNullOrWhiteSpace(_operatorStateService.StreamTitle),
                hint: "Checks when a staged pre-live title is present."),
            CreateStreamInfoItem(
                "stream-info.category",
                "Category/game set",
                sortOrder: 101,
                isChecked: !string.IsNullOrWhiteSpace(_operatorStateService.StreamCategory),
                hint: "Checks when a staged pre-live category is present."),
            CreateManualReminderItem(),
            CreateManualItem("obs-scene-ready", "OBS scene configured and active", true, ObsTechnicalCategory, 200, "Confirm the correct scene collection is live in OBS."),
            CreateManualItem("ingest-url-copied", "RTMP ingest URL configured in OBS", true, ObsTechnicalCategory, 201, "Copy the ingest URL into OBS, then leave this checked once the target is saved.", _streamingOptions.Value.IngestUrl, true),
            CreateManualItem("audio-levels-set", "Audio levels checked", true, ObsTechnicalCategory, 202, null),
            CreateManualItem("test-stream-done", "Test stream completed", false, ObsTechnicalCategory, 203, null),
            .. BuildRecordingItems(recordingStatus),
            .. BuildOverlayItems(),
            .. BuildPersonalPrepItems()
        ];

        return [.. items.OrderBy(static item => item.Definition.SortOrder)];
    }

    private IEnumerable<ChecklistItemState> BuildOverlayItems()
    {
        int sortOrder = 300;

        foreach (ITestableOverlayComponent component in GetTestableOverlayComponents())
        {
            string itemId = BuildOverlayItemId(component.ComponentName);
            bool isChecked = TryGetCheckedAt(itemId, out DateTimeOffset checkedAt);

            yield return new ChecklistItemState
            {
                Definition = new ChecklistItemDefinition
                {
                    Id = itemId,
                    Category = OverlayVerificationCategory,
                    Label = $"{component.ComponentName} visible",
                    Type = ChecklistItemType.Action,
                    IsRequired = false,
                    Hint = "Run the built-in test flash and the checklist will confirm this item after the overlay settles.",
                    ActionLabel = "Test",
                    SortOrder = sortOrder++
                },
                IsChecked = isChecked,
                CheckedAt = isChecked ? checkedAt : null
            };
        }
    }

    private IReadOnlyList<ITestableOverlayComponent> GetTestableOverlayComponents()
    {
        return _overlayService.GetComponents()
            .OfType<ITestableOverlayComponent>()
            .OrderBy(static component => component.ComponentName, StringComparer.Ordinal)
            .ToArray();
    }

    private bool TryResolveOverlayComponentName(string itemId, out string componentName)
    {
        string? resolvedComponentName = GetTestableOverlayComponents()
            .FirstOrDefault(component => string.Equals(BuildOverlayItemId(component.ComponentName), itemId, StringComparison.OrdinalIgnoreCase))
            ?.ComponentName;

        componentName = resolvedComponentName ?? string.Empty;
        return !string.IsNullOrWhiteSpace(resolvedComponentName);
    }

    private IEnumerable<ChecklistItemState> BuildPlatformConnectionItems()
    {
        int sortOrder = 0;

        foreach (IPlatformConnection platformConnection in GetVisiblePlatformConnections())
        {
            yield return new ChecklistItemState
            {
                Definition = new ChecklistItemDefinition
                {
                    Id = BuildPlatformConnectionItemId(platformConnection.PlatformName),
                    Category = PlatformConnectionsCategory,
                    Label = $"{platformConnection.PlatformName} connected",
                    Type = ChecklistItemType.Auto,
                    IsRequired = true,
                    Hint = "Enabled platforms must be connected before going live.",
                    SortOrder = sortOrder++
                },
                IsChecked = platformConnection.State == PlatformConnectionState.Connected,
                IsAutoChecked = platformConnection.State == PlatformConnectionState.Connected,
                IsBlocked = platformConnection.State == PlatformConnectionState.Error,
                IsWarning = platformConnection.State == PlatformConnectionState.Error && !string.IsNullOrWhiteSpace(platformConnection.LastError),
                WarningMessage = platformConnection.State == PlatformConnectionState.Error
                    ? platformConnection.LastError
                    : null
            };
        }
    }

    private ChecklistItemState CreateManualItem(
        string itemId,
        string label,
        bool isRequired,
        string category,
        int sortOrder,
        string? hint,
        string? inlineValue = null,
        bool canCopyInlineValue = false)
    {
        bool isChecked = TryGetCheckedAt(itemId, out DateTimeOffset checkedAt);

        return new ChecklistItemState
        {
            Definition = new ChecklistItemDefinition
            {
                Id = itemId,
                Category = category,
                Label = label,
                Type = ChecklistItemType.Manual,
                IsRequired = isRequired,
                Hint = hint,
                InlineValue = inlineValue,
                CanCopyInlineValue = canCopyInlineValue,
                SortOrder = sortOrder
            },
            IsChecked = isChecked,
            CheckedAt = isChecked ? checkedAt : null
        };
    }

    private IEnumerable<ChecklistItemState> BuildPersonalPrepItems()
    {
        List<ChecklistItemDefinition> personalPrepDefinitions = GetPersonalPrepDefinitions();

        foreach (ChecklistItemDefinition definition in personalPrepDefinitions)
        {
            bool isChecked = TryGetCheckedAt(definition.Id, out DateTimeOffset checkedAt);

            yield return new ChecklistItemState
            {
                Definition = definition,
                IsChecked = isChecked,
                CheckedAt = isChecked ? checkedAt : null
            };
        }
    }

    private ChecklistItemState CreateManualReminderItem()
    {
        IReadOnlyList<PlatformManualReminder> visibleReminders = GetVisibleReminders();
        bool isChecked = visibleReminders.Count > 0 && _operatorStateService.AreAllManualRemindersReviewed(visibleReminders);

        return new ChecklistItemState
        {
            Definition = new ChecklistItemDefinition
            {
                Id = "stream-info.manual-reminders",
                Category = StreamInfoCategory,
                Label = "Platform manual settings reviewed",
                Type = ChecklistItemType.Action,
                IsRequired = true,
                Hint = "Open the stream info dialog and confirm each platform-specific reminder.",
                ActionLabel = "Review",
                SortOrder = 102
            },
            IsChecked = isChecked
        };
    }

    private IEnumerable<ChecklistItemState> BuildRecordingItems(RecordingStorageStatus recordingStatus)
    {
        yield return new ChecklistItemState
        {
            Definition = new ChecklistItemDefinition
            {
                Id = "recording-path-configured",
                Category = RecordingCategory,
                Label = "Recording output path configured",
                Type = ChecklistItemType.Auto,
                IsRequired = false,
                Hint = "Checks when the configured recording folder is available.",
                SortOrder = 206
            },
            IsChecked = recordingStatus.IsPathConfigured,
            IsAutoChecked = recordingStatus.IsPathConfigured,
            IsWarning = !recordingStatus.IsPathConfigured && !string.IsNullOrWhiteSpace(recordingStatus.PathWarningMessage),
            WarningMessage = recordingStatus.PathWarningMessage
        };

        yield return new ChecklistItemState
        {
            Definition = new ChecklistItemDefinition
            {
                Id = "recording-disk-space",
                Category = RecordingCategory,
                Label = "Sufficient disk space (≥ 10 GB free)",
                Type = ChecklistItemType.AutoWithWarn,
                IsRequired = false,
                Hint = "Warns without blocking go live when the recording drive gets tight.",
                SortOrder = 207
            },
            IsChecked = recordingStatus.HasSufficientDiskSpace,
            IsAutoChecked = recordingStatus.HasSufficientDiskSpace,
            IsWarning = !recordingStatus.HasSufficientDiskSpace && !string.IsNullOrWhiteSpace(recordingStatus.DiskSpaceWarningMessage),
            WarningMessage = recordingStatus.DiskSpaceWarningMessage
        };
    }

    private ChecklistItemState CreateStreamInfoItem(string itemId, string label, int sortOrder, bool isChecked, string? hint)
    {
        return new ChecklistItemState
        {
            Definition = new ChecklistItemDefinition
            {
                Id = itemId,
                Category = StreamInfoCategory,
                Label = label,
                Type = ChecklistItemType.Auto,
                IsRequired = true,
                Hint = hint,
                SortOrder = sortOrder
            },
            IsChecked = isChecked,
            IsAutoChecked = isChecked
        };
    }

    private IReadOnlyList<IPlatformConnection> GetVisiblePlatformConnections()
    {
        return _platformConnections
            .Where(static platformConnection =>
                platformConnection.State != PlatformConnectionState.Disabled &&
                platformConnection.State != PlatformConnectionState.PendingApproval)
            .OrderBy(static platformConnection => platformConnection.PlatformName, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<PlatformManualReminder> GetVisibleReminders()
    {
        HashSet<string> visiblePlatforms =
        [
            .. GetVisiblePlatformConnections().Select(static platformConnection => platformConnection.PlatformName)
        ];

        return _manualReminderProvider
            .GetReminders()
            .Where(reminder => visiblePlatforms.Contains(reminder.Platform))
            .ToArray();
    }

    private void HandleDependencyStateChanged(object? sender, EventArgs args)
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SubscribeToPlatformEvents(IEnumerable<IPlatformConnection> platformConnections)
    {
        foreach (IPlatformConnection platformConnection in platformConnections)
        {
            switch (platformConnection)
            {
                case ITwitchService twitchService:
                    EventHandler<TwitchConnectionState> twitchHandler = (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
                    twitchService.ConnectionStateChanged += twitchHandler;
                    _unsubscribeActions.Add(() => twitchService.ConnectionStateChanged -= twitchHandler);
                    break;
                case IYouTubeService youTubeService:
                    EventHandler<YouTubeConnectionState> youTubeHandler = (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
                    youTubeService.ConnectionStateChanged += youTubeHandler;
                    _unsubscribeActions.Add(() => youTubeService.ConnectionStateChanged -= youTubeHandler);
                    break;
                case IFacebookService facebookService:
                    EventHandler<FacebookConnectionState> facebookHandler = (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
                    facebookService.ConnectionStateChanged += facebookHandler;
                    _unsubscribeActions.Add(() => facebookService.ConnectionStateChanged -= facebookHandler);
                    break;
                case IDiscordService discordService:
                    EventHandler<DiscordConnectionState> discordHandler = (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
                    discordService.ConnectionStateChanged += discordHandler;
                    _unsubscribeActions.Add(() => discordService.ConnectionStateChanged -= discordHandler);
                    break;
                case IXService xService:
                    EventHandler<XConnectionState> xHandler = (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
                    xService.ConnectionStateChanged += xHandler;
                    _unsubscribeActions.Add(() => xService.ConnectionStateChanged -= xHandler);
                    break;
            }
        }
    }

    private List<ChecklistItemDefinition> GetPersonalPrepDefinitions()
    {
        lock (_personalPrepDefinitionsLock)
        {
            return [.. _personalPrepDefinitions];
        }
    }

    private RecordingStorageStatus GetRecordingStatus()
    {
        RecordingStorageStatus status = _recordingStorageProbe.GetStatus();

        lock (_recordingStatusLock)
        {
            _lastObservedRecordingStatus = status;
        }

        return status;
    }

    private async Task PollRecordingState(CancellationToken cancellationToken)
    {
        EvaluateRecordingStatusChange();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_recordingPollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            EvaluateRecordingStatusChange();
        }
    }

    private void EvaluateRecordingStatusChange()
    {
        RecordingStorageStatus currentStatus = _recordingStorageProbe.GetStatus();
        bool changed;

        lock (_recordingStatusLock)
        {
            changed = currentStatus != _lastObservedRecordingStatus;
            _lastObservedRecordingStatus = currentStatus;
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool TryGetCheckedAt(string itemId, out DateTimeOffset checkedAt)
    {
        lock (_checkedItemsLock)
        {
            return _checkedItems.TryGetValue(itemId, out checkedAt);
        }
    }

    private static List<ChecklistItemDefinition> CreateDefaultPersonalPrepDefinitions()
    {
        return BuildPersonalPrepDefinitions(CreateDefaultCatalogItems());
    }

    private static List<ChecklistItemDefinition> BuildPersonalPrepDefinitions(IEnumerable<CustomChecklistItemDefinition> customItems)
    {
        return
        [
            .. customItems
                .Where(static item => item.IsEnabled)
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id)
                .Select(item => new ChecklistItemDefinition
                {
                    Id = $"personal-prep-{item.Id}",
                    Category = PersonalPrepCategory,
                    Label = item.Label,
                    Type = ChecklistItemType.Manual,
                    IsRequired = false,
                    SortOrder = 400 + item.DisplayOrder
                })
        ];
    }

    private static List<CustomChecklistItemDefinition> CreateDefaultCatalogItems()
    {
        return
        [
            new CustomChecklistItemDefinition
            {
                Id = 1,
                Label = "Drink water",
                DisplayOrder = 1,
                IsEnabled = true
            },
            new CustomChecklistItemDefinition
            {
                Id = 2,
                Label = "Close email and notifications",
                DisplayOrder = 2,
                IsEnabled = true
            },
            new CustomChecklistItemDefinition
            {
                Id = 3,
                Label = "Confirm stream schedule posted",
                DisplayOrder = 3,
                IsEnabled = true
            }
        ];
    }

    private static bool IsPersonalPrepItemId(string itemId)
    {
        return itemId.StartsWith("personal-prep-", StringComparison.OrdinalIgnoreCase) ||
               itemId.StartsWith("personal.", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticCustomChecklistItemCatalog : ICustomChecklistItemCatalog
    {
        private readonly IReadOnlyList<CustomChecklistItemDefinition> _items;

        public StaticCustomChecklistItemCatalog(IReadOnlyList<CustomChecklistItemDefinition> items)
        {
            _items = items;
        }

        public Task<IReadOnlyList<CustomChecklistItemDefinition>> List(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(_items);
        }
    }
}
