using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Modules.Control.Components.TopBar;

namespace Thiccdal.Tests;

public sealed class TopBarTests
{
    [Fact]
    public void WhenRequiredItemsRemainUnchecked_ThenGoLiveButtonIsDisabledAndBadgeShowsPluralRemainingText()
    {
        using TopBarTestHarness harness = new();

        IRenderedComponent<TopBar> cut = harness.Render();

        Assert.True(cut.Find("button.topbar__go-live").HasAttribute("disabled"));
        Assert.Equal("✗ 6 items remaining", cut.Find(".topbar__go-live-badge").TextContent.Trim());
    }

    [Fact]
    public void WhenOneRequiredItemRemains_ThenBadgeShowsSingularRemainingText()
    {
        using TopBarTestHarness harness = new();
        harness.SatisfyAllRequiredItemsExcept("audio-levels-set");

        IRenderedComponent<TopBar> cut = harness.Render();

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("button.topbar__go-live").HasAttribute("disabled"));
            Assert.Equal("✗ 1 item remaining", cut.Find(".topbar__go-live-badge").TextContent.Trim());
        });
    }

    [Fact]
    public void WhenLastRequiredItemIsSatisfied_ThenGoLiveButtonEnablesAndBadgeShowsReadyText()
    {
        using TopBarTestHarness harness = new();
        IRenderedComponent<TopBar> cut = harness.Render();

        harness.SatisfyAllRequiredItemsExcept("audio-levels-set");
        harness.ChecklistService.SetItemChecked("audio-levels-set", true);

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("button.topbar__go-live").HasAttribute("disabled"));
            Assert.Equal("✓ Ready to go live", cut.Find(".topbar__go-live-badge").TextContent.Trim());
        });
    }

    [Fact]
    public void WhenRequiredItemRevertsToPending_ThenGoLiveButtonBecomesDisabled()
    {
        using TopBarTestHarness harness = new();
        IRenderedComponent<TopBar> cut = harness.Render();

        harness.SatisfyAllRequiredItems();

        cut.WaitForAssertion(() => Assert.False(cut.Find("button.topbar__go-live").HasAttribute("disabled")));

        harness.OperatorStateService.SetStreamInfo(string.Empty, "Science & Technology", []);

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("button.topbar__go-live").HasAttribute("disabled"));
            Assert.Equal("✗ 1 item remaining", cut.Find(".topbar__go-live-badge").TextContent.Trim());
        });
    }

    [Fact]
    public void WhenRunningInDevelopment_ThenPreviewLiveDashboardButtonRenders()
    {
        using TopBarTestHarness harness = new();
        IRenderedComponent<TopBar> cut = harness.Render();

        Assert.Contains("Preview Live Dashboard", cut.Markup);
    }

    [Fact]
    public void WhenNotRunningInDevelopment_ThenPreviewLiveDashboardButtonDoesNotRender()
    {
        using TopBarTestHarness harness = new("Production");
        IRenderedComponent<TopBar> cut = harness.Render();

        Assert.DoesNotContain("Preview Live Dashboard", cut.Markup);
    }

    [Fact]
    public void WhenPreviewLiveDashboardClickedInDevelopment_ThenOperatorTransitionsToLiveWithoutExecutingGoLiveAction()
    {
        using TopBarTestHarness harness = new();
        IRenderedComponent<TopBar> cut = harness.Render();

        cut.FindAll("button")
            .Single(static button => button.TextContent.Contains("Preview Live Dashboard", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(OperatorMode.Live, harness.OperatorStateService.Mode);
            Assert.Equal(0, harness.GoLiveActionService.ExecuteCount);
            Assert.Contains("Go Offline", cut.Markup);
        });
    }

    private sealed class TopBarTestHarness : IDisposable
    {
        private readonly TestContext _context = new();

        public TopBarTestHarness(string environmentName = "Development")
        {
            OperatorStateService = new OperatorStateService();
            TwitchService = new FakeTwitchService(PlatformConnectionState.Connected);
            ChecklistService = new PreLiveChecklistService(
                OperatorStateService,
                [TwitchService],
                new FakeOverlayService(),
                new FakePlatformManualReminderProvider(),
                new FakeRecordingStorageProbe(new RecordingStorageStatus(true, null, true, null)),
                Options.Create(new StreamingOptions
                {
                    IngestUrl = "rtmp://localhost:1935/live"
                }),
                TimeProvider.System);
            GoLiveActionService = new FakeGoLiveActionService();

            _context.Services.AddSingleton<IOperatorStateService>(OperatorStateService);
            _context.Services.AddSingleton<IPreLiveChecklistService>(ChecklistService);
            _context.Services.AddSingleton<IGoLiveActionService>(GoLiveActionService);
            _context.Services.AddSingleton<ILogger<TopBar>>(NullLogger<TopBar>.Instance);
            _context.Services.AddSingleton<IPlatformConnection>(TwitchService);
            _context.Services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment(environmentName));
            _context.Services.AddSingleton<IEmoteRenderingOptions>(new Mock<IEmoteRenderingOptions>().Object);
            _context.Services.AddSingleton<ITwitchStreamInfoService>(new Mock<ITwitchStreamInfoService>().Object);
        }

        public OperatorStateService OperatorStateService { get; }

        public PreLiveChecklistService ChecklistService { get; }

        public FakeTwitchService TwitchService { get; }

        public FakeGoLiveActionService GoLiveActionService { get; }

        public IRenderedComponent<TopBar> Render()
        {
            return _context.RenderComponent<TopBar>();
        }

        public void SatisfyAllRequiredItems()
        {
            SatisfyAllRequiredItemsExcept();
        }

        public void SatisfyAllRequiredItemsExcept(params string[] excludedItemIds)
        {
            HashSet<string> excludedIds = [.. excludedItemIds];

            if (!excludedIds.Contains("stream-info.title") || !excludedIds.Contains("stream-info.category"))
            {
                OperatorStateService.SetStreamInfo(
                    excludedIds.Contains("stream-info.title") ? string.Empty : "Ship it",
                    excludedIds.Contains("stream-info.category") ? string.Empty : "Science & Technology",
                    []);
            }

            if (!excludedIds.Contains("stream-info.manual-reminders"))
            {
                OperatorStateService.SetManualReminderReviewed("Twitch", "Visibility", true);
            }

            foreach (string itemId in new[] { "obs-scene-ready", "ingest-url-copied", "audio-levels-set" })
            {
                if (!excludedIds.Contains(itemId))
                {
                    ChecklistService.SetItemChecked(itemId, true);
                }
            }
        }

        public void Dispose()
        {
            ChecklistService.Dispose();
            OperatorStateService.Dispose();
            _context.Dispose();
        }
    }

    private sealed class FakeGoLiveActionService : IGoLiveActionService
    {
        public int ExecuteCount { get; private set; }

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public GoLiveActionState GetState()
        {
            return new GoLiveActionState();
        }

        public Task Execute(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            ExecuteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string ApplicationName { get; set; } = "Thiccdal.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; }

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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
        private readonly RecordingStorageStatus _status;

        public FakeRecordingStorageProbe(RecordingStorageStatus status)
        {
            _status = status;
        }

        public RecordingStorageStatus GetStatus()
        {
            return _status;
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

        public event EventHandler<TwitchConnectionState>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

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
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task RefreshStreamState(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Connect(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            _ = message;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
