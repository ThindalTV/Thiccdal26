using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    private const string OverlayVerificationCategory = "Overlay Verification";
    private const string PersonalPrepCategory = "Personal Prep";

    private readonly IOperatorStateService _operatorStateService;
    private readonly IOverlayService _overlayService;
    private readonly IPlatformManualReminderProvider _manualReminderProvider;
    private readonly ICustomChecklistItemCatalog _customChecklistItemCatalog;
    private readonly IReadOnlyList<IPlatformConnection> _platformConnections;
    private readonly Lock _checkedItemsLock = new();
    private readonly Lock _personalPrepDefinitionsLock = new();
    private readonly Dictionary<string, DateTimeOffset> _checkedItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Action> _unsubscribeActions = [];
    private List<ChecklistItemDefinition> _personalPrepDefinitions = CreateDefaultPersonalPrepDefinitions();
    private readonly TimeSpan _overlayActionCompletionDelay;


    [ActivatorUtilitiesConstructor]
    public PreLiveChecklistService(
        IOperatorStateService operatorStateService,
        IEnumerable<IPlatformConnection> platformConnections,
        IOverlayService overlayService,
        IPlatformManualReminderProvider manualReminderProvider,
        ICustomChecklistItemCatalog customChecklistItemCatalog,
        TimeProvider timeProvider)
        : this(
            operatorStateService,
            platformConnections,
            overlayService,
            manualReminderProvider,
            customChecklistItemCatalog,
            timeProvider,
            DefaultOverlayActionCompletionDelay)
    {
    }

    public PreLiveChecklistService(
        IOperatorStateService operatorStateService,
        IEnumerable<IPlatformConnection> platformConnections,
        IOverlayService overlayService,
        IPlatformManualReminderProvider manualReminderProvider,
        ICustomChecklistItemCatalog customChecklistItemCatalog,
        TimeProvider timeProvider,
        TimeSpan overlayActionCompletionDelay)
    {
        ArgumentNullException.ThrowIfNull(customChecklistItemCatalog);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(overlayActionCompletionDelay, TimeSpan.Zero);

        _operatorStateService = operatorStateService;
        _overlayService = overlayService;
        _manualReminderProvider = manualReminderProvider;
        _customChecklistItemCatalog = customChecklistItemCatalog;
        _platformConnections = platformConnections.ToArray();
        _overlayActionCompletionDelay = overlayActionCompletionDelay;

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
        await Reload(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
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
            CreateManualItem("audio-levels-set", "Audio levels checked", true, ObsTechnicalCategory, 202, null),
            CreateManualItem("test-stream-done", "Test stream completed", false, ObsTechnicalCategory, 203, null),
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
