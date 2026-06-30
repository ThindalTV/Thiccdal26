using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class ChatAggregationServiceTests
{
    [Fact]
    public async Task WhenHostedServiceStartsAndStops_ThenConnectionsAreConnectedAndDisconnected()
    {
        TestPlatformConnection firstConnection = new();
        TestPlatformConnection secondConnection = new();
        RecordingChatPersistenceService chatPersistenceService = new();
        RecordingEventPersistenceService eventPersistenceService = new();
        ChatAggregationService service = CreateService(
            [firstConnection, secondConnection],
            chatPersistenceService,
            eventPersistenceService,
            new NoOpCommandDispatcher());

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, firstConnection.ConnectCalls);
        Assert.Equal(1, secondConnection.ConnectCalls);
        Assert.Equal(1, firstConnection.DisconnectCalls);
        Assert.Equal(1, secondConnection.DisconnectCalls);
    }

    [Fact]
    public async Task WhenChatEventIsNotPersisted_ThenItIsPersistedBeforeSubscribersReceiveIt()
    {
        TestPlatformConnection connection = new();
        RecordingChatPersistenceService chatPersistenceService = new();
        RecordingEventPersistenceService eventPersistenceService = new();
        ChatAggregationService service = CreateService(
            [connection],
            chatPersistenceService,
            eventPersistenceService,
            new NoOpCommandDispatcher());

        await service.StartAsync(CancellationToken.None);
        using CancellationTokenSource subscriberCancellation = new(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<ChatEvent> subscriber = service.Subscribe(subscriberCancellation.Token)
            .GetAsyncEnumerator(subscriberCancellation.Token);
        Task<bool> moveNextTask = subscriber.MoveNextAsync().AsTask();

        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.Twitch, "chat-1", "viewer", "How is it going?");

        connection.Emit(chatEvent);

        Assert.True(await moveNextTask);
        Assert.Equal(1, chatPersistenceService.PersistCalls);
        Assert.Equal(0, eventPersistenceService.PersistCalls);
        Assert.True(subscriber.Current.PersistedRecordId > 0);
        Assert.Same(chatEvent, subscriber.Current);
        Assert.Equal(["persist:chat-1"], chatPersistenceService.CallOrder);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenChatEventIsAlreadyPersisted_ThenAggregationSkipsRepersistingIt()
    {
        TestPlatformConnection connection = new();
        RecordingChatPersistenceService chatPersistenceService = new();
        RecordingEventPersistenceService eventPersistenceService = new();
        ChatAggregationService service = CreateService(
            [connection],
            chatPersistenceService,
            eventPersistenceService,
            new NoOpCommandDispatcher());

        await service.StartAsync(CancellationToken.None);
        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.Twitch, "chat-2", "viewer", "Hello there") with
        {
            PersistedRecordId = 42
        };

        connection.Emit(chatEvent);
        await chatPersistenceService.WaitForIdle();

        Assert.Equal(0, chatPersistenceService.PersistCalls);
        Assert.Equal(0, eventPersistenceService.PersistCalls);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenMultipleSubscribersListen_ThenEachReceivesEveryChatEvent()
    {
        TestPlatformConnection connection = new();
        ChatAggregationService service = CreateService(
            [connection],
            new RecordingChatPersistenceService(),
            new RecordingEventPersistenceService(),
            new NoOpCommandDispatcher());

        await service.StartAsync(CancellationToken.None);
        using CancellationTokenSource firstCancellation = new(TimeSpan.FromSeconds(5));
        using CancellationTokenSource secondCancellation = new(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<ChatEvent> firstSubscriber = service.Subscribe(firstCancellation.Token)
            .GetAsyncEnumerator(firstCancellation.Token);
        await using IAsyncEnumerator<ChatEvent> secondSubscriber = service.Subscribe(secondCancellation.Token)
            .GetAsyncEnumerator(secondCancellation.Token);
        Task<bool> firstMoveNextTask = firstSubscriber.MoveNextAsync().AsTask();
        Task<bool> secondMoveNextTask = secondSubscriber.MoveNextAsync().AsTask();

        connection.Emit(CreateChatEvent(PlatformEventSource.Twitch, "chat-3", "viewer", "Hi"));

        Assert.True(await firstMoveNextTask);
        Assert.True(await secondMoveNextTask);
        Assert.Equal("chat-3", firstSubscriber.Current.ExternalId);
        Assert.Equal("chat-3", secondSubscriber.Current.ExternalId);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenChatArrivesFromMultiplePlatforms_ThenSubscribersReceiveMergedMessages()
    {
        TestPlatformConnection twitchConnection = new();
        TestPlatformConnection youtubeConnection = new();
        ChatAggregationService service = CreateService(
            [twitchConnection, youtubeConnection],
            new RecordingChatPersistenceService(),
            new RecordingEventPersistenceService(),
            new NoOpCommandDispatcher());

        await service.StartAsync(CancellationToken.None);
        using CancellationTokenSource subscriberCancellation = new(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<ChatEvent> subscriber = service.Subscribe(subscriberCancellation.Token)
            .GetAsyncEnumerator(subscriberCancellation.Token);
        Task<bool> firstMoveNextTask = subscriber.MoveNextAsync().AsTask();

        twitchConnection.Emit(CreateChatEvent(PlatformEventSource.Twitch, "twitch-1", "viewer-1", "hello from twitch"));

        Assert.True(await firstMoveNextTask);
        ChatEvent firstMessage = subscriber.Current;
        Task<bool> secondMoveNextTask = subscriber.MoveNextAsync().AsTask();

        youtubeConnection.Emit(CreateChatEvent(PlatformEventSource.YouTube, "youtube-1", "viewer-2", "hello from youtube"));

        Assert.True(await secondMoveNextTask);
        ChatEvent secondMessage = subscriber.Current;

        Assert.Equal(PlatformEventSource.Twitch, firstMessage.Source);
        Assert.Equal("twitch-1", firstMessage.ExternalId);
        Assert.Equal(PlatformEventSource.YouTube, secondMessage.Source);
        Assert.Equal("youtube-1", secondMessage.ExternalId);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenOnePlatformDisconnects_ThenOtherPlatformsStillDispatchChat()
    {
        TestPlatformConnection disconnectedConnection = new();
        TestPlatformConnection healthyConnection = new();
        ChatAggregationService service = CreateService(
            [disconnectedConnection, healthyConnection],
            new RecordingChatPersistenceService(),
            new RecordingEventPersistenceService(),
            new NoOpCommandDispatcher());

        await service.StartAsync(CancellationToken.None);
        using CancellationTokenSource subscriberCancellation = new(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<ChatEvent> subscriber = service.Subscribe(subscriberCancellation.Token)
            .GetAsyncEnumerator(subscriberCancellation.Token);
        Task<bool> moveNextTask = subscriber.MoveNextAsync().AsTask();

        await disconnectedConnection.Disconnect(CancellationToken.None);
        healthyConnection.Emit(CreateChatEvent(PlatformEventSource.YouTube, "youtube-2", "viewer-3", "still here"));

        Assert.True(await moveNextTask);
        ChatEvent receivedEvent = subscriber.Current;

        Assert.Equal(PlatformEventSource.YouTube, receivedEvent.Source);
        Assert.Equal("youtube-2", receivedEvent.ExternalId);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenSubscriberFallsBehind_ThenOldestMessagesAreDroppedAndWarningIsLogged()
    {
        TestPlatformConnection connection = new();
        RecordingLogger<ChatAggregationService> logger = new();
        ChatAggregationService service = CreateService(
            [connection],
            new RecordingChatPersistenceService(),
            new RecordingEventPersistenceService(),
            new NoOpCommandDispatcher(),
            logger);

        await service.StartAsync(CancellationToken.None);
        using CancellationTokenSource subscriberCancellation = new(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<ChatEvent> subscriber = service.Subscribe(subscriberCancellation.Token)
            .GetAsyncEnumerator(subscriberCancellation.Token);
        Task<bool> firstMoveNextTask = subscriber.MoveNextAsync().AsTask();

        for (int index = 1; index <= 514; index++)
        {
            connection.Emit(CreateChatEvent(PlatformEventSource.Twitch, $"chat-{index}", "viewer", $"message-{index}"));
        }

        Assert.True(await firstMoveNextTask);
        ChatEvent firstDeliveredEvent = subscriber.Current;
        List<ChatEvent> bufferedEvents = await ReadNextChats(subscriber, 512);

        Assert.Equal("chat-1", firstDeliveredEvent.ExternalId);
        Assert.Equal(512, bufferedEvents.Count);
        Assert.Equal("chat-3", bufferedEvents[0].ExternalId);
        Assert.Equal("chat-514", bufferedEvents[^1].ExternalId);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Warning && entry.Message.Contains("Dropping oldest aggregated chat event", StringComparison.Ordinal));

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenChatCommandArrives_ThenDispatcherRunsAfterPersistence()
    {
        TestPlatformConnection connection = new();
        RecordingChatPersistenceService chatPersistenceService = new();
        RecordingCommandDispatcher commandDispatcher = new();
        ChatAggregationService service = CreateService(
            [connection],
            chatPersistenceService,
            new RecordingEventPersistenceService(),
            commandDispatcher);

        await service.StartAsync(CancellationToken.None);

        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.Twitch, "chat-command", "viewer", "!hello");
        connection.Emit(chatEvent);
        await chatPersistenceService.WaitForIdle();

        Assert.Single(commandDispatcher.DispatchCalls);
        Assert.True(commandDispatcher.DispatchCalls[0].PersistedRecordId > 0);

        await service.StopAsync(CancellationToken.None);
    }

    private static ChatAggregationService CreateService(
        IEnumerable<IPlatformConnection> platformConnections,
        IChatPersistenceService chatPersistenceService,
        IEventPersistenceService eventPersistenceService,
        ICommandDispatcher commandDispatcher,
        ILogger<ChatAggregationService>? logger = null)
    {
        ServiceCollection services = new();
        services.AddSingleton(chatPersistenceService);
        services.AddSingleton(eventPersistenceService);
        services.AddScoped<IChatPersistenceService>(_ => chatPersistenceService);
        services.AddScoped<IEventPersistenceService>(_ => eventPersistenceService);
        ServiceProvider provider = services.BuildServiceProvider();

        return new ChatAggregationService(
            platformConnections,
            provider.GetRequiredService<IServiceScopeFactory>(),
            commandDispatcher,
            logger ?? new RecordingLogger<ChatAggregationService>());
    }

    private static ChatEvent CreateChatEvent(
        PlatformEventSource source,
        string externalId,
        string author,
        string content)
    {
        return new ChatEvent
        {
            Source = source,
            Type = PlatformEventType.ChatMessage,
            Author = author,
            Channel = "thiccdal",
            ExternalId = externalId,
            Summary = $"{author} sent chat",
            Content = content,
            RawData = $"{{\"payload\":{{\"event\":{{\"user_id\":\"{author}-id\"}}}}}}"
        };
    }

    private static async Task<ChatEvent> ReadNextChat(IAsyncEnumerator<ChatEvent> subscriber)
    {
        Assert.True(await subscriber.MoveNextAsync());
        return subscriber.Current;
    }

    private static async Task<List<ChatEvent>> ReadNextChats(IAsyncEnumerator<ChatEvent> subscriber, int count)
    {
        List<ChatEvent> chatEvents = [];
        for (int index = 0; index < count; index++)
        {
            chatEvents.Add(await ReadNextChat(subscriber));
        }

        return chatEvents;
    }

    private sealed class RecordingChatPersistenceService : IChatPersistenceService
    {
        private readonly ConcurrentQueue<ChatEvent> _persistedEvents = new();

        public List<string> CallOrder { get; } = [];

        public int PersistCalls => _persistedEvents.Count;

        public Task Persist(ChatEvent chatEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            chatEvent.PersistedRecordId = _persistedEvents.Count + 1;
            _persistedEvents.Enqueue(chatEvent);
            CallOrder.Add($"persist:{chatEvent.ExternalId}");
            return Task.CompletedTask;
        }

        public Task WaitForIdle()
        {
            return Task.Delay(50);
        }
    }

    private sealed class RecordingEventPersistenceService : IEventPersistenceService
    {
        private readonly ConcurrentQueue<PlatformEvent> _persistedEvents = new();

        public int PersistCalls => _persistedEvents.Count;

        public Task Persist(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            platformEvent.PersistedRecordId = _persistedEvents.Count + 1;
            _persistedEvents.Enqueue(platformEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpCommandDispatcher : ICommandDispatcher
    {
        public Task Dispatch(ChatEvent chatEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCommandDispatcher : ICommandDispatcher
    {
        public List<ChatEvent> DispatchCalls { get; } = [];

        public Task Dispatch(ChatEvent chatEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DispatchCalls.Add(chatEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class TestPlatformConnection : IPlatformConnection
    {
        public int ConnectCalls { get; private set; }

        public int DisconnectCalls { get; private set; }

        public string PlatformName { get; init; } = "Test";

        public PlatformConnectionState State => Connected
            ? PlatformConnectionState.Connected
            : PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool Connected { get; private set; }

        public event EventHandler<ChatEvent>? OnChatMessageReceived;

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

        public Task Connect(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connected = true;
            ConnectCalls++;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connected = false;
            DisconnectCalls++;
            return Task.CompletedTask;
        }

        public void Emit(PlatformEvent platformEvent)
        {
            OnPlatformEventReceived?.Invoke(this, platformEvent);
            if (platformEvent is ChatEvent chatEvent)
            {
                OnChatMessageReceived?.Invoke(this, chatEvent);
            }
        }

        public string GetAuthorizationUrl()
        {
            return string.Empty;
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

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
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);
}
