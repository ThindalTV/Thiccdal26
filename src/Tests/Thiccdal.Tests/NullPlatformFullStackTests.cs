using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.API.Status;
using Thiccdal.Data;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Infrastructure.YouTube;
using Thiccdal.Remote.Null;
using PersistedPlatformEvent = Thiccdal.Data.Models.PlatformEvent;
using PersistedSubscribeEvent = Thiccdal.Data.Models.SubscribeEvent;
using Xunit.Abstractions;

namespace Thiccdal.Tests;

public sealed class NullPlatformFullStackTests
{
    private readonly ITestOutputHelper _output;

    public NullPlatformFullStackTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task WhenNullPlatformPublishesChatEvent_ThenAggregatedChatPersistsIntoSqlite()
    {
        await using NullPlatformApplicationFactory factory = new(_output);
        using HttpClient client = factory.CreateHttpsClient();
        Assert.NotNull(client.BaseAddress);

        NullPlatformConnection platformConnection = factory.Services.GetRequiredService<NullPlatformConnection>();
        IChatAggregationService chatAggregationService = factory.Services.GetRequiredService<IChatAggregationService>();
        using CancellationTokenSource subscriberCancellation = new(TimeSpan.FromSeconds(1));
        await using IAsyncEnumerator<ChatEvent> subscriber = chatAggregationService.Subscribe(subscriberCancellation.Token)
            .GetAsyncEnumerator(subscriberCancellation.Token);
        Task<bool> moveNextTask = subscriber.MoveNextAsync().AsTask();

        ChatEvent chatEvent = CreateChatEvent("null-chat-1", "Alice", "Hello from the Null platform");

        await platformConnection.PublishEvent(chatEvent, subscriberCancellation.Token);

        Assert.True(await moveNextTask);
        Assert.Same(chatEvent, subscriber.Current);
        Assert.True(subscriber.Current.PersistedRecordId > 0);

        await using ApplicationDbContext dbContext = await factory.CreateDbContext();
        Thiccdal.Data.Models.ChatMessage storedMessage = await dbContext.ChatMessages
            .Include(chatMessage => chatMessage.PlatformEvent)
            .Include(chatMessage => chatMessage.PlatformUser)
            .SingleAsync(message => message.PlatformEvent.ExternalId == "null-chat-1");

        Assert.True(storedMessage.Id > 0);
        Assert.Equal("Hello from the Null platform", storedMessage.Content);
        Assert.Equal(subscriber.Current.PersistedRecordId, storedMessage.PlatformEventId);
        Assert.Equal("alice-id", storedMessage.PlatformUser.PlatformUserId);
        Assert.Equal("Alice", storedMessage.PlatformUser.DisplayName);
    }

    [Fact]
    public async Task WhenEventBusPublishesSubscribeEvent_ThenItPersistsBeforeSubscribersReceiveIt()
    {
        await using NullPlatformApplicationFactory factory = new(_output);
        using HttpClient client = factory.CreateHttpsClient();
        Assert.NotNull(client.BaseAddress);

        IEventBus eventBus = factory.Services.GetRequiredService<IEventBus>();
        using CancellationTokenSource subscriptionCancellation = new(TimeSpan.FromSeconds(1));
        await using IAsyncEnumerator<PlatformEvent> subscriber = eventBus.Subscribe(subscriptionCancellation.Token)
            .GetAsyncEnumerator(subscriptionCancellation.Token);
        Task<bool> moveNextTask = subscriber.MoveNextAsync().AsTask();
        TwitchSubscribeEvent subscribeEvent = new()
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Subscribe,
            SourceEventType = "channel.subscribe",
            Author = "Alice",
            Channel = "thiccdal",
            ExternalId = "sub-1",
            Summary = "Alice subscribed",
            RawData = "{\"subscription\":{\"tier\":\"Tier 1\"}}",
            OccurredAt = DateTime.UtcNow,
            Tier = "Tier 1"
        };

        await eventBus.Publish(subscribeEvent, subscriptionCancellation.Token);

        Assert.True(await moveNextTask);
        Assert.Same(subscribeEvent, subscriber.Current);
        Assert.True(subscriber.Current.PersistedRecordId > 0);

        await using ApplicationDbContext dbContext = await factory.CreateDbContext();
        PersistedPlatformEvent storedEvent = await dbContext.PlatformEvents.SingleAsync(platformEvent => platformEvent.ExternalId == "sub-1");
        string eventType = await dbContext.PlatformEvents
            .Where(platformEvent => platformEvent.ExternalId == "sub-1")
            .Select(platformEvent => EF.Property<string>(platformEvent, "EventType"))
            .SingleAsync();

        Assert.Equal(subscribeEvent.PersistedRecordId, storedEvent.Id);
        Assert.Equal("SubscribeEvent", eventType);
        Assert.IsType<PersistedSubscribeEvent>(storedEvent);
    }

    [Fact]
    public async Task WhenNullPlatformReceivesHelloCommand_ThenReplyIsRoutedBackToThatPlatformChannel()
    {
        await using NullPlatformApplicationFactory factory = new(_output);
        using HttpClient client = factory.CreateHttpsClient();
        Assert.NotNull(client.BaseAddress);

        IBotCommandManagementService botCommandManagementService = factory.Services.GetRequiredService<IBotCommandManagementService>();
        _ = await botCommandManagementService.Create(
            new BotCommandDefinitionInput("!hello", "Hello {user}!", null, true),
            CancellationToken.None);

        NullPlatformConnection platformConnection = factory.Services.GetRequiredService<NullPlatformConnection>();
        ChatEvent chatEvent = CreateChatEvent("null-chat-command-1", "Alice", "!hello");

        await platformConnection.PublishEvent(chatEvent, CancellationToken.None);

        TestLogEntry replyLog = await factory.LogSink.WaitForEntry(
            entry => entry.Category.EndsWith(nameof(NullPlatformConnection), StringComparison.Ordinal) &&
                     entry.Message.Contains("Hello Alice!", StringComparison.Ordinal) &&
                     entry.Message.Contains("Null/null-room", StringComparison.Ordinal),
            TimeSpan.FromSeconds(1));

        Assert.Equal(LogLevel.Information, replyLog.LogLevel);

        await using ApplicationDbContext dbContext = await factory.CreateDbContext();
        Thiccdal.Data.Models.BotCommand storedCommand = await dbContext.BotCommands.SingleAsync(command => command.Trigger == "!hello");
        Assert.Equal(1, storedCommand.UseCount);
    }

    [Fact]
    public async Task WhenOperatorSetsActiveLiveStream_ThenStatusEndpointReportsOnlineForNullPlatform()
    {
        await using NullPlatformApplicationFactory factory = new(_output);
        using HttpClient client = factory.CreateHttpsClient();

        NullPlatformConnection platformConnection = factory.Services.GetRequiredService<NullPlatformConnection>();
        IOperatorStateService operatorStateService = factory.Services.GetRequiredService<IOperatorStateService>();

        await platformConnection.Connect(CancellationToken.None);
        operatorStateService.SetActiveStreamState(
            new OperatorStreamState
            {
                Title = "Null platform smoke test",
                Category = "Science & Technology",
                Tags = ["null", "integration"],
                StartedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(5))
            });

        StreamStatusResponse? payload = await client.GetFromJsonAsync<StreamStatusResponse>("/status");

        Assert.NotNull(payload);
        Assert.Equal(StreamStatusStates.Online, payload.State);
        Assert.Contains(payload.Platforms, static platform => platform is { Name: "Null", State: "Connected" });
    }

    [Fact]
    public async Task WhenDashboardRequestedWithNullPlatformHost_ThenApplicationShellRenders()
    {
        await using NullPlatformApplicationFactory factory = new(_output);
        using HttpClient client = factory.CreateHttpsClient();

        HttpResponseMessage response = await client.GetAsync("/dashboard");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();
        Assert.Contains("THICCDAL", content);
        Assert.Contains("Pre-Live Checklist", content);
        Assert.Contains("Overlay Test Area", content);
        Assert.Contains("Go Live", content);
    }

    private static ChatEvent CreateChatEvent(string externalId, string author, string content)
    {
        return new ChatEvent
        {
            Source = PlatformEventSource.Null,
            Type = PlatformEventType.ChatMessage,
            SourceEventType = "null.chat",
            Author = author,
            Channel = "null-room",
            ExternalId = externalId,
            Summary = $"{author} sent chat",
            Content = content,
            RawData = $"{{\"payload\":{{\"event\":{{\"user_id\":\"{author.ToLowerInvariant()}-id\"}}}}}}",
            OccurredAt = DateTime.UtcNow
        };
    }

    private sealed class NullPlatformApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly ITestOutputHelper _output;

        public NullPlatformApplicationFactory(ITestOutputHelper output)
        {
            _output = output;
            DatabasePath = Path.Combine(AppContext.BaseDirectory, $"thiccdal-null-full-stack-{Guid.NewGuid():N}.db");
            LogSink = new TestLogSink();
        }

        public string DatabasePath { get; }

        public TestLogSink LogSink { get; }

        public HttpClient CreateHttpsClient()
        {
            _output.WriteLine($"Null full-stack SQLite path: {DatabasePath}");

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

        public override async ValueTask DisposeAsync()
        {
            try
            {
                await base.DisposeAsync();
            }
            catch (ObjectDisposedException exception) when (string.Equals(exception.ObjectName, "System.Threading.SemaphoreSlim", StringComparison.Ordinal))
            {
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.AddProvider(new TestLoggerProvider(LogSink)));
            builder.ConfigureAppConfiguration(
                (_, configurationBuilder) =>
                {
                    Dictionary<string, string?> settings = new()
                    {
                        ["ConnectionStrings:DefaultConnection"] = $"Data Source={DatabasePath}",
                        ["Twitch:ClientId"] = "null-full-stack-client-id",
                        ["Twitch:ClientSecret"] = "null-full-stack-client-secret",
                        ["Twitch:RedirectUri"] = "https://localhost/auth/twitch/callback",
                        ["Null:PlatformName"] = "Null",
                        ["Null:AuthorizationUrl"] = "https://example.test/null"
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
                    services.RemoveAll<PreLiveChecklistService>();
                    services.RemoveAll<IPreLiveChecklistService>();
                    services.RemoveAll<ITwitchService>();
                    services.RemoveAll<ITwitchConnectionMonitor>();
                    services.RemoveAll<ITwitchTokenManager>();
                    services.RemoveAll<IYouTubeService>();
                    services.RemoveAll<IYouTubeConnectionMonitor>();
                    services.RemoveAll<IYouTubeTokenManager>();

                    services.AddNullIntegration(context.Configuration);
                    services.AddSingleton<ITwitchService>(new TestTwitchService());
                    services.AddSingleton<ITwitchConnectionMonitor>(new TestTwitchConnectionMonitor());
                    services.AddSingleton<ITwitchTokenManager>(new TestTwitchTokenManager());
                    services.AddSingleton<IYouTubeService>(new TestYouTubeService());
                    services.AddSingleton<IYouTubeConnectionMonitor>(new TestYouTubeConnectionMonitor());
                    services.AddSingleton<IYouTubeTokenManager>(new TestYouTubeTokenManager());
                    services.AddSingleton<IRecordingStorageProbe>(
                        new StaticRecordingStorageProbe(new RecordingStorageStatus(
                            false,
                            "Set a recording output folder to enable local capture.",
                            false,
                            "Recording drive monitoring starts after a recording folder is configured.")));
                    services.Configure<StreamingOptions>(_ => { });
                    services.AddSingleton<PreLiveChecklistService>(
                        serviceProvider => new PreLiveChecklistService(
                            serviceProvider.GetRequiredService<IOperatorStateService>(),
                            serviceProvider.GetServices<IPlatformConnection>(),
                            serviceProvider.GetRequiredService<IOverlayService>(),
                            serviceProvider.GetRequiredService<IPlatformManualReminderProvider>(),
                            serviceProvider.GetRequiredService<IRecordingStorageProbe>(),
                            serviceProvider.GetRequiredService<IOptions<StreamingOptions>>(),
                            serviceProvider.GetRequiredService<TimeProvider>()));
                    services.AddSingleton<IPreLiveChecklistService>(
                        serviceProvider => serviceProvider.GetRequiredService<PreLiveChecklistService>());
                });
        }
    }

    private sealed class TestLogSink
    {
        private readonly ConcurrentQueue<TestLogEntry> _entries = new();

        public void Add(TestLogEntry entry)
        {
            _entries.Enqueue(entry);
        }

        public async Task<TestLogEntry> WaitForEntry(Func<TestLogEntry, bool> predicate, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow <= deadline)
            {
                TestLogEntry? match = _entries.FirstOrDefault(predicate);
                if (match is not null)
                {
                    return match;
                }

                await Task.Delay(20);
            }

            throw new Xunit.Sdk.XunitException("Timed out waiting for a matching log entry.");
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

    private sealed record TestLogEntry(string Category, LogLevel LogLevel, string Message);

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly TestLogSink _sink;

        public TestLoggerProvider(TestLogSink sink)
        {
            _sink = sink;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(categoryName, _sink);
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly TestLogSink _sink;

        public TestLogger(string categoryName, TestLogSink sink)
        {
            _categoryName = categoryName;
            _sink = sink;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
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
            _sink.Add(new TestLogEntry(_categoryName, logLevel, formatter(state, exception)));
        }
    }

    private sealed class TestTwitchService : ITwitchService
    {
        public string PlatformName => "Twitch";

        public TwitchConnectionState ConnectionState => TwitchConnectionState.NotAuthorized;

        public PlatformConnectionState State => PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool IsStreamLive => false;

        public TwitchStreamState StreamState => new();

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

        public Task Connect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Disconnect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshStreamState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTitle(string title, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetDescription(string description, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetCategory(string category, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

        public string GetAuthorizationUrl() => "https://id.twitch.tv/oauth2/authorize";

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestTwitchTokenManager : ITwitchTokenManager
    {
        public Task<string?> GetToken(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<bool> HasToken(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task RefreshToken(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoreToken(string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Revoke(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetAuthorizationUrl() => "https://id.twitch.tv/oauth2/authorize";

        public bool ValidateAndConsumeState(string state) => true;
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

        public Task Connect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Disconnect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshStreamState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTitle(string title, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetDescription(string description, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetCategory(string category, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

        public string GetAuthorizationUrl() => "https://accounts.google.com/o/oauth2/auth";

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestYouTubeTokenManager : IYouTubeTokenManager
    {
        public string GetAuthorizationUrl() => "https://accounts.google.com/o/oauth2/auth";

        public bool ValidateAndConsumeState(string state) => true;

        public Task StoreToken(string authorizationCode, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetToken(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<bool> HasToken(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task RevokeToken(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
