using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Thiccdal.API.Status;
using Thiccdal.Data;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Infrastructure.YouTube;
using Thiccdal.Remote.Null;

namespace Thiccdal.Tests;

public sealed class GoLiveIntegrationTests
{
    [Fact]
    public async Task WhenGoLiveRunsThroughHost_ThenStreamingStartsModeTransitionsAndChecklistSnapshotPersists()
    {
        await using GoLiveApplicationFactory factory = new();
        using HttpClient client = factory.CreateHttpsClient();
        Assert.NotNull(client.BaseAddress);

        NullPlatformConnection platformConnection = factory.Services.GetRequiredService<NullPlatformConnection>();
        IOperatorStateService operatorStateService = factory.Services.GetRequiredService<IOperatorStateService>();
        IPreLiveChecklistService checklistService = factory.Services.GetRequiredService<IPreLiveChecklistService>();
        IPlatformManualReminderProvider reminderProvider = factory.Services.GetRequiredService<IPlatformManualReminderProvider>();
        IGoLiveActionService goLiveActionService = factory.Services.GetRequiredService<IGoLiveActionService>();
        IStreamingService streamingService = factory.Services.GetRequiredService<IStreamingService>();
        IRtmpFanoutService fanoutService = factory.Services.GetRequiredService<IRtmpFanoutService>();

        await SatisfyRequiredItems(checklistService, operatorStateService, platformConnection, reminderProvider);
        PreLiveChecklistState preGoLiveState = checklistService.GetState();

        await goLiveActionService.Execute();

        OperatorStreamState activeStreamState = Assert.IsType<OperatorStreamState>(operatorStateService.GetActiveStreamState());
        Assert.True(streamingService.IsRunning);
        Assert.True(fanoutService.IsRunning);
        Assert.Equal(OperatorMode.Live, operatorStateService.Mode);
        Assert.Equal("Null platform go-live integration", activeStreamState.Title);
        Assert.Equal("Science & Technology", activeStreamState.Category);
        Assert.Equal(["integration", "go-live"], activeStreamState.Tags);

        PreLiveChecklistState resetState = checklistService.GetState();
        Assert.False(resetState.AllRequiredChecked);
        Assert.False(GetItem(resetState, "stream-info.manual-reminders").IsChecked);
        Assert.False(GetItem(resetState, "obs-scene-ready").IsChecked);
        Assert.False(GetItem(resetState, "ingest-url-copied").IsChecked);
        Assert.False(GetItem(resetState, "audio-levels-set").IsChecked);

        await using ApplicationDbContext dbContext = await factory.CreateDbContext();
        Thiccdal.Data.Models.ChecklistSession persistedSession = Assert.Single(
            await dbContext.ChecklistSessions
                .Include(session => session.Items)
                .ToListAsync());

        Assert.Equal(activeStreamState.SessionId, persistedSession.SessionId);
        Assert.Equal(preGoLiveState.Items.Count, persistedSession.Items.Count);
        Assert.Contains(
            persistedSession.Items,
            static item => item.ItemId == "obs-scene-ready" && item.Status == "Checked");
        Assert.Contains(
            persistedSession.Items,
            static item => item.ItemId == "platform-connection.null" && item.Status == "Checked");
    }

    [Fact]
    public async Task WhenGoLiveRunsThroughHost_ThenStatusEndpointReportsOnlineCurrentStreamingState()
    {
        await using GoLiveApplicationFactory factory = new();
        using HttpClient client = factory.CreateHttpsClient();

        NullPlatformConnection platformConnection = factory.Services.GetRequiredService<NullPlatformConnection>();
        IOperatorStateService operatorStateService = factory.Services.GetRequiredService<IOperatorStateService>();
        IPreLiveChecklistService checklistService = factory.Services.GetRequiredService<IPreLiveChecklistService>();
        IPlatformManualReminderProvider reminderProvider = factory.Services.GetRequiredService<IPlatformManualReminderProvider>();
        IGoLiveActionService goLiveActionService = factory.Services.GetRequiredService<IGoLiveActionService>();

        await SatisfyRequiredItems(checklistService, operatorStateService, platformConnection, reminderProvider);

        await goLiveActionService.Execute();

        StreamStatusResponse payload = Assert.IsType<StreamStatusResponse>(
            await client.GetFromJsonAsync<StreamStatusResponse>("/status"));
        StreamInfoDto stream = Assert.IsType<StreamInfoDto>(payload.Stream);

        Assert.Equal(StreamStatusStates.Online, payload.State);
        Assert.Equal("Null platform go-live integration", stream.Title);
        Assert.Equal("Science & Technology", stream.Category);
        Assert.Equal(["integration", "go-live"], stream.Tags);
        Assert.Contains(payload.Platforms, static platform => platform is { Name: "Null", State: "Connected" });
    }

    private static async Task SatisfyRequiredItems(
        IPreLiveChecklistService checklistService,
        IOperatorStateService operatorStateService,
        NullPlatformConnection platformConnection,
        IPlatformManualReminderProvider reminderProvider)
    {
        await platformConnection.Connect(CancellationToken.None);

        operatorStateService.SetStreamInfo(
            "Null platform go-live integration",
            "Science & Technology",
            ["integration", "go-live"]);

        foreach (PlatformManualReminder reminder in reminderProvider.GetReminders().Where(static reminder => reminder.Platform == "Null"))
        {
            operatorStateService.SetManualReminderReviewed(reminder.Platform, reminder.Setting, true);
        }

        foreach (ChecklistItemState item in checklistService.GetState().Items.Where(static item => item.Definition.IsRequired && item.Definition.Type == ChecklistItemType.Manual))
        {
            checklistService.SetItemChecked(item.Definition.Id, true);
        }

        Assert.True(checklistService.AllRequiredChecked);
    }

    private static ChecklistItemState GetItem(PreLiveChecklistState state, string itemId)
    {
        return Assert.Single(state.Items, item => item.Definition.Id == itemId);
    }

    private sealed class GoLiveApplicationFactory : WebApplicationFactory<Program>
    {
        public GoLiveApplicationFactory()
        {
            DatabasePath = Path.Combine(AppContext.BaseDirectory, $"thiccdal-go-live-integration-{Guid.NewGuid():N}.db");
        }

        public string DatabasePath { get; }

        public HttpClient CreateHttpsClient()
        {
            return CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost")
                });
        }

        public Task<ApplicationDbContext> CreateDbContext(CancellationToken cancellationToken = default)
        {
            return Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync(cancellationToken);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(
                (_, configurationBuilder) =>
                {
                    Dictionary<string, string?> settings = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = $"Data Source={DatabasePath}",
                        ["Twitch:ClientId"] = "go-live-test-client-id",
                        ["Twitch:ClientSecret"] = "go-live-test-client-secret",
                        ["Twitch:RedirectUri"] = "https://localhost/auth/twitch/callback",
                        ["Null:PlatformName"] = "Null",
                        ["Null:AuthorizationUrl"] = "https://example.test/null",
                        ["Null:RtmpRelayUrl"] = "rtmp://localhost:1936/live/null",
                        ["Streaming:IngestUrl"] = "rtmp://localhost:1935/live/go-live-integration"
                    };

                    configurationBuilder.AddInMemoryCollection(settings);
                });

            builder.ConfigureServices(
                (context, services) =>
                {
                    services.RemoveAll<IPlatformConnection>();
                    services.RemoveAll<IChatSource>();
                    services.RemoveAll<IStreamTarget>();
                    services.RemoveAll<IEventSource>();
                    services.RemoveAll<IPlatformEventSource>();
                    services.RemoveAll<IIntegrationConnectionMonitor>();
                    services.RemoveAll<ITwitchService>();
                    services.RemoveAll<ITwitchConnectionMonitor>();
                    services.RemoveAll<ITwitchTokenManager>();
                    services.RemoveAll<IYouTubeService>();
                    services.RemoveAll<IYouTubeConnectionMonitor>();
                    services.RemoveAll<IYouTubeTokenManager>();
                    services.RemoveAll<IPlatformManualReminderProvider>();
                    services.RemoveAll<IRecordingStorageProbe>();

                    services.AddNullIntegration(context.Configuration);
                    services.AddSingleton<ITwitchService>(new TestTwitchService());
                    services.AddSingleton<ITwitchConnectionMonitor>(new TestTwitchConnectionMonitor());
                    services.AddSingleton<ITwitchTokenManager>(new TestTwitchTokenManager());
                    services.AddSingleton<IYouTubeService>(new TestYouTubeService());
                    services.AddSingleton<IYouTubeConnectionMonitor>(new TestYouTubeConnectionMonitor());
                    services.AddSingleton<IYouTubeTokenManager>(new TestYouTubeTokenManager());
                    services.AddSingleton<IPlatformManualReminderProvider>(new TestPlatformManualReminderProvider());
                    services.AddSingleton<IRecordingStorageProbe>(
                        new StaticRecordingStorageProbe(
                            new RecordingStorageStatus(
                                false,
                                "Set a recording output folder to enable local capture.",
                                false,
                                "Recording drive monitoring starts after a recording folder is configured.")));
                });
        }
    }

    private sealed class StaticRecordingStorageProbe : IRecordingStorageProbe
    {
        private readonly RecordingStorageStatus _status;

        public StaticRecordingStorageProbe(RecordingStorageStatus status)
        {
            _status = status;
        }

        public RecordingStorageStatus GetStatus()
        {
            return _status;
        }
    }

    private sealed class TestPlatformManualReminderProvider : IPlatformManualReminderProvider
    {
        public IReadOnlyList<PlatformManualReminder> GetReminders()
        {
            return
            [
                new PlatformManualReminder
                {
                    Platform = "Null",
                    Setting = "Visibility",
                    ReminderText = "Confirm the null integration is ready for the go-live flow."
                }
            ];
        }
    }

    private sealed class TestTwitchConnectionMonitor : ITwitchConnectionMonitor
    {
        public string PlatformName => "Twitch";

        public bool IsConnected => false;

        public event EventHandler? ConnectionChanged
        {
            add { }
            remove { }
        }

        public string GetAuthorizationUrl()
        {
            return "https://example.test/twitch";
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestTwitchTokenManager : ITwitchTokenManager
    {
        public Task<string?> GetToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<string?>(null);
        }

        public Task<bool> HasToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(false);
        }

        public Task RefreshToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task StoreToken(string code, CancellationToken cancellationToken = default)
        {
            _ = code;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Revoke(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public string GetAuthorizationUrl()
        {
            return "https://example.test/twitch";
        }

        public bool ValidateAndConsumeState(string state)
        {
            _ = state;
            return true;
        }
    }

    private sealed class TestTwitchService : ITwitchService
    {
        public string PlatformName => "Twitch";

        public TwitchConnectionState ConnectionState => TwitchConnectionState.NotAuthorized;

        public PlatformConnectionState State => PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool IsStreamLive => false;

        public TwitchStreamState StreamState => new TwitchStreamState();

        public bool Connected => false;

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
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
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

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            _ = message;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestYouTubeConnectionMonitor : IYouTubeConnectionMonitor
    {
        public string PlatformName => "YouTube";

        public bool IsConnected => false;

        public event EventHandler? ConnectionChanged
        {
            add { }
            remove { }
        }

        public string GetAuthorizationUrl()
        {
            return "https://example.test/youtube";
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestYouTubeTokenManager : IYouTubeTokenManager
    {
        public string GetAuthorizationUrl()
        {
            return "https://example.test/youtube";
        }

        public bool ValidateAndConsumeState(string state)
        {
            _ = state;
            return true;
        }

        public Task StoreToken(string authorizationCode, CancellationToken cancellationToken = default)
        {
            _ = authorizationCode;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task<string?> GetToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<string?>(null);
        }

        public Task<bool> HasToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(false);
        }

        public Task RevokeToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestYouTubeService : IYouTubeService
    {
        public string PlatformName => "YouTube";

        public YouTubeConnectionState ConnectionState => YouTubeConnectionState.NotAuthorized;

        public PlatformConnectionState State => PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool IsStreamLive => false;

        public YouTubeBroadcastInfo? ActiveBroadcast => null;

        public bool Connected => false;

        public event EventHandler<YouTubeConnectionState>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<bool>? StreamLiveStateChanged
        {
            add { }
            remove { }
        }

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
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
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

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            _ = message;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task SetTitle(string title, CancellationToken cancellationToken = default)
        {
            _ = title;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task SetDescription(string description, CancellationToken cancellationToken = default)
        {
            _ = description;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task SetCategory(string category, CancellationToken cancellationToken = default)
        {
            _ = category;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
