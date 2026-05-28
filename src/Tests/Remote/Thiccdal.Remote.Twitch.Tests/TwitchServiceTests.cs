using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Data;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;
using PersistedFollowEvent = Thiccdal.Data.Models.FollowEvent;
using PersistedPlatformEvent = Thiccdal.Data.Models.PlatformEvent;
using RuntimePlatformEvent = Thiccdal.Infrastructure.Bot.Models.PlatformEvent;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchServiceTests
{
    [Fact]
    public async Task WhenBroadcasterHasActiveStream_ThenIsStreamLiveIsTrue()
    {
        var service = CreateService(
            helixClient: new FakeHelixClient
            {
                StreamState = new TwitchStreamState
                {
                    IsLive = true
                }
            });

        await service.RefreshStreamState();

        Assert.True(service.IsStreamLive);
    }

    [Fact]
    public async Task WhenEventSubDeliversChatMessage_ThenChatIsPersistedBeforeDispatch()
    {
        DbContextOptions<ApplicationDbContext> dbOptions = BuildOptions();
        var eventSubClient = new FakeEventSubClient();
        var service = CreateService(eventSubClient: eventSubClient, dbContextFactory: new TestDbContextFactory(dbOptions));
        TaskCompletionSource<ChatEvent> receivedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        service.OnChatMessageRecieved += (_, chatEvent) => receivedEvent.TrySetResult(chatEvent);

        await service.Connect();
        eventSubClient.Emit(new ChatEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.ChatMessage,
            Author = "viewer",
            Channel = "thindal",
            ExternalId = "message-1",
            Summary = "Kappa hi",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"payload\":{\"event\":{\"chatter_user_id\":\"user-42\"}}}",
            Content = "Kappa hi",
            HtmlContent = "<img alt=\"Kappa\" /> hi"
        });

        ChatEvent dispatchedEvent = await WaitAsync(receivedEvent.Task);
        Assert.Equal("viewer", dispatchedEvent.Author);

        await using var dbContext = new ApplicationDbContext(dbOptions);
        PersistedPlatformEvent storedEvent = await dbContext.PlatformEvents.SingleAsync();
        var storedChatMessage = await dbContext.ChatMessages.Include(chatMessage => chatMessage.PlatformUser).SingleAsync();
        var storedPlatformUser = await dbContext.PlatformUsers.SingleAsync();
        Assert.Equal(PlatformEventType.ChatMessage, storedEvent.Type);
        Assert.Equal("Kappa hi", storedEvent.Content);
        Assert.Equal("<img alt=\"Kappa\" /> hi", storedEvent.HtmlContent);
        Assert.Equal(storedEvent.Id, storedChatMessage.PlatformEventId);
        Assert.Equal(storedPlatformUser.Id, storedChatMessage.PlatformUserId);
        Assert.Equal("Kappa hi", storedChatMessage.Content);
        Assert.Equal("user-42", storedPlatformUser.PlatformUserId);
        Assert.Equal("viewer", storedPlatformUser.DisplayName);
    }

    [Fact]
    public async Task WhenEventSubDeliversFollow_ThenPlatformEventIsPersistedBeforeDispatch()
    {
        DbContextOptions<ApplicationDbContext> dbOptions = BuildOptions();
        var eventSubClient = new FakeEventSubClient();
        var service = CreateService(eventSubClient: eventSubClient, dbContextFactory: new TestDbContextFactory(dbOptions));
        TaskCompletionSource<RuntimePlatformEvent> receivedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        service.OnPlatformEventReceived += (_, platformEvent) =>
        {
            if (platformEvent is not ChatEvent)
            {
                receivedEvent.TrySetResult(platformEvent);
            }
        };

        await service.Connect();
        eventSubClient.Emit(new TwitchFollowEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Follow,
            Author = "newviewer",
            Channel = "thindal",
            ExternalId = "follow-1",
            Summary = "newviewer followed thindal",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"id\":\"follow-1\"}",
            FollowerUserId = "123"
        });

        RuntimePlatformEvent dispatchedEvent = await WaitAsync(receivedEvent.Task);
        Assert.Equal(PlatformEventType.Follow, dispatchedEvent.Type);

        await using var dbContext = new ApplicationDbContext(dbOptions);
        PersistedFollowEvent storedEvent = await dbContext.PlatformEvents.OfType<PersistedFollowEvent>().SingleAsync();
        Assert.Equal(PlatformEventType.Follow, storedEvent.Type);
        Assert.Equal("newviewer followed thindal", storedEvent.Summary);
        Assert.Equal(string.Empty, storedEvent.Content);
    }

    [Fact]
    public async Task WhenRefreshingConnectionState_ThenStateRefreshDoesNotRequireStreamLookup()
    {
        var helixClient = new FakeHelixClient();
        var service = CreateService(helixClient: helixClient);

        await service.RefreshConnectionState();

        Assert.Equal(TwitchConnectionState.Authorized, service.ConnectionState);
        Assert.False(service.IsStreamLive);
        Assert.Null(helixClient.StreamStateProfile);
    }

    [Fact]
    public async Task WhenNoTokenExists_ThenConnectLeavesServiceNotAuthorized()
    {
        var service = CreateService(tokenManager: new MissingTokenManager());

        await service.Connect();

        Assert.Equal(TwitchConnectionState.NotAuthorized, service.ConnectionState);
        Assert.False(service.Connected);
    }

    [Fact]
    public async Task WhenNoTokenExists_ThenRefreshStreamStateLeavesStreamOffline()
    {
        var service = CreateService(tokenManager: new MissingTokenManager());

        await service.RefreshStreamState();

        Assert.Equal(TwitchConnectionState.NotAuthorized, service.ConnectionState);
        Assert.False(service.IsStreamLive);
    }

    [Fact]
    public async Task WhenRefreshingStreamState_ThenUsesConfiguredTargetBroadcasterId()
    {
        var helixClient = new FakeHelixClient();
        var targetChannelService = new TestTargetChannelService(new TwitchChatConnectionProfile
        {
            BotUsername = "riverbot",
            BotUserId = "24680",
            TargetChannel = "guestchannel",
            BroadcasterId = "98765"
        });

        var service = CreateService(
            helixClient: helixClient,
            targetChannelService: targetChannelService);

        await service.RefreshStreamState();

        Assert.Equal("98765", helixClient.StreamStateProfile?.BroadcasterId);
    }

    [Fact]
    public async Task WhenSendingMessage_ThenHelixUsesResolvedBotAndBroadcasterIds()
    {
        var helixClient = new FakeHelixClient
        {
            SendMessageResult = new TwitchSendMessageResult
            {
                IsSuccessful = true,
                MessageId = "message-1"
            }
        };
        var targetChannelService = new TestTargetChannelService(new TwitchChatConnectionProfile
        {
            BotUsername = "riverbot",
            BotUserId = "24680",
            TargetChannel = "guestchannel",
            BroadcasterId = "98765"
        });

        var service = CreateService(
            helixClient: helixClient,
            targetChannelService: targetChannelService);

        await service.SendMessage("hello from helix");

        Assert.Equal("hello from helix", helixClient.LastChatMessage);
        Assert.Equal("24680", helixClient.SendMessageProfile?.BotUserId);
        Assert.Equal("98765", helixClient.SendMessageProfile?.BroadcasterId);
    }

    [Fact]
    public async Task WhenUpdatingStreamInfoWithSupportedFields_ThenHelixReceivesTitleAndCategory()
    {
        FakeHelixClient helixClient = new();
        TwitchService service = CreateService(helixClient: helixClient);

        StreamInfoUpdateResult result = await service.UpdateStreamInfo(new StreamInfoUpdateRequest
        {
            Title = "Pre-live title",
            Category = "Just Chatting"
        });

        Assert.Equal(StreamInfoUpdateStatus.Succeeded, result.Status);
        Assert.Equal("Pre-live title", helixClient.LastUpdatedTitle);
        Assert.Equal("Just Chatting", helixClient.LastUpdatedCategory);
    }

    [Fact]
    public async Task WhenUpdatingStreamInfoWithTags_ThenResultIsPartialSuccess()
    {
        FakeHelixClient helixClient = new();
        TwitchService service = CreateService(helixClient: helixClient);

        StreamInfoUpdateResult result = await service.UpdateStreamInfo(new StreamInfoUpdateRequest
        {
            Title = "Pre-live title",
            Tags = ["dotnet", "blazor"]
        });

        Assert.Equal(StreamInfoUpdateStatus.PartiallySucceeded, result.Status);
        Assert.Contains("Tags are not supported by Twitch Helix channel updates.", result.Message);
        Assert.Equal("Pre-live title", helixClient.LastUpdatedTitle);
    }

    [Fact]
    public async Task WhenUpdatingStreamInfoWithTagsOnly_ThenResultIsUnsupported()
    {
        TwitchService service = CreateService(helixClient: new FakeHelixClient());

        StreamInfoUpdateResult result = await service.UpdateStreamInfo(new StreamInfoUpdateRequest
        {
            Tags = ["dotnet"]
        });

        Assert.Equal(StreamInfoUpdateStatus.Unsupported, result.Status);
    }

    private static DbContextOptions<ApplicationDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static TwitchService CreateService(
        ITwitchTokenManager? tokenManager = null,
        ITwitchTargetChannelService? targetChannelService = null,
        ITwitchHelixClient? helixClient = null,
        ITwitchEventSubClient? eventSubClient = null,
        IDbContextFactory<ApplicationDbContext>? dbContextFactory = null)
    {
        var options = Options.Create(new TwitchOptions());

        DbContextOptions<ApplicationDbContext> dbOptions = BuildOptions();
        IDbContextFactory<ApplicationDbContext> resolvedDbContextFactory = dbContextFactory ?? new TestDbContextFactory(dbOptions);
        ServiceCollection services = new();
        services.AddSingleton(new ChatPersistenceService(resolvedDbContextFactory, new NullLogger<ChatPersistenceService>()));
        services.AddScoped<IChatPersistenceService>(serviceProvider => serviceProvider.GetRequiredService<ChatPersistenceService>());
        services.AddScoped<IEventPersistenceService>(serviceProvider => new EventPersistenceService(
            resolvedDbContextFactory,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new NullLogger<EventPersistenceService>()));
        services.AddSingleton<IEventBus, EventBus>();
        ServiceProvider provider = services.BuildServiceProvider();

        return new TwitchService(
            options,
            tokenManager ?? new TestTokenManager(),
            targetChannelService ?? new TestTargetChannelService(new TwitchChatConnectionProfile
            {
                BotUsername = "thindalbot",
                BotUserId = "24680",
                TargetChannel = "thindal",
                BroadcasterId = "12345"
            }),
            helixClient ?? new FakeHelixClient(),
            eventSubClient ?? new FakeEventSubClient(),
            provider.GetRequiredService<IEventBus>(),
            new NullLogger<TwitchService>());
    }

    private static async Task<T> WaitAsync<T>(Task<T> task)
    {
        Task completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, completedTask);
        return await task;
    }

    private sealed class TestTokenManager : ITwitchTokenManager
    {
        public Task<string?> GetToken(CancellationToken cancellationToken = default) => Task.FromResult<string?>("token");

        public Task<bool> HasToken(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task RefreshToken(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoreToken(string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Revoke(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetAuthorizationUrl() => string.Empty;

        public bool ValidateAndConsumeState(string state) => true;
    }

    private sealed class TestTargetChannelService : ITwitchTargetChannelService
    {
        private TwitchChatConnectionProfile _profile;

        public event EventHandler<TwitchChatConnectionProfile>? ConnectionProfileChanged;

        public TestTargetChannelService(TwitchChatConnectionProfile profile)
        {
            _profile = profile;
        }

        public Task<TwitchChatConnectionProfile> GetConnectionProfile(CancellationToken cancellationToken = default) =>
            Task.FromResult(_profile);

        public Task<TwitchChatConnectionProfile> UpdateTargetChannel(
            TwitchTargetChannelSettings targetChannel,
            CancellationToken cancellationToken = default)
        {
            _profile = new TwitchChatConnectionProfile
            {
                BotUsername = _profile.BotUsername,
                BotUserId = _profile.BotUserId,
                TargetChannel = targetChannel.TargetChannel,
                BroadcasterId = targetChannel.BroadcasterId
            };

            ConnectionProfileChanged?.Invoke(this, _profile);
            return Task.FromResult(_profile);
        }
    }

    private sealed class MissingTokenManager : ITwitchTokenManager
    {
        public Task<string?> GetToken(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<bool> HasToken(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task RefreshToken(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoreToken(string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Revoke(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetAuthorizationUrl() => string.Empty;

        public bool ValidateAndConsumeState(string state) => true;
    }

    private sealed class FakeHelixClient : ITwitchHelixClient
    {
        public TwitchStreamState StreamState { get; init; } = new TwitchStreamState();

        public TwitchSendMessageResult SendMessageResult { get; init; } = new TwitchSendMessageResult
        {
            IsSuccessful = true,
            MessageId = "message-1"
        };

        public IReadOnlyList<TwitchEventSubSubscription> EventSubscriptions { get; init; } = [];

        public TwitchChatConnectionProfile? StreamStateProfile { get; private set; }

        public TwitchChatConnectionProfile? SendMessageProfile { get; private set; }

        public string? LastChatMessage { get; private set; }

        public string? LastUpdatedTitle { get; private set; }

        public string? LastUpdatedCategory { get; private set; }

        public Task<TwitchSendMessageResult> SendChatMessage(
            TwitchChatConnectionProfile profile,
            string message,
            CancellationToken cancellationToken = default)
        {
            SendMessageProfile = profile;
            LastChatMessage = message;
            return Task.FromResult(SendMessageResult);
        }

        public Task<TwitchStreamState> GetStreamState(
            TwitchChatConnectionProfile profile,
            CancellationToken cancellationToken = default)
        {
            StreamStateProfile = profile;
            return Task.FromResult(StreamState);
        }

        public Task<IReadOnlyList<TwitchEventSubSubscription>> GetEventSubscriptions(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(EventSubscriptions);
        }

        public Task UpdateChannelInfo(
            TwitchChatConnectionProfile profile,
            string? title,
            string? category,
            CancellationToken cancellationToken = default)
        {
            StreamStateProfile = profile;
            LastUpdatedTitle = title;
            LastUpdatedCategory = category;
            return Task.CompletedTask;
        }

        public Task CreateEventSubscription(TwitchEventSubSubscriptionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<TwitchUser?> GetAuthenticatedUser(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<TwitchUser?>(null);
        }
    }

    private sealed class FakeEventSubClient : ITwitchEventSubClient
    {
        public bool Connected { get; private set; }

        public event EventHandler<Thiccdal.Infrastructure.Bot.Models.PlatformEvent>? OnEventReceived;
        public event EventHandler<ChatEvent>? ChatMessageReceived;
        public event EventHandler<Thiccdal.Infrastructure.Bot.Models.PlatformEvent>? PlatformEventReceived;
        public event EventHandler? Disconnected;
        public event EventHandler<Exception>? Faulted
        {
            add { }
            remove { }
        }

        public Task Connect(TwitchChatConnectionProfile profile, CancellationToken cancellationToken = default)
        {
            Connected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            Connected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Emit(Thiccdal.Infrastructure.Bot.Models.PlatformEvent platformEvent)
        {
            OnEventReceived?.Invoke(this, platformEvent);
            if (platformEvent is ChatEvent chatEvent)
            {
                ChatMessageReceived?.Invoke(this, chatEvent);
                return;
            }

            PlatformEventReceived?.Invoke(this, platformEvent);
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext() => new ApplicationDbContext(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(_options));
    }
}
