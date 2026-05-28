using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class ChatRepostServiceTests
{
    [Fact]
    public async Task WhenChatMessageIsReceived_ThenItIsRepostedToOtherPlatforms()
    {
        TestPlatformConnection twitchConnection = new() { TestPlatformSource = PlatformEventSource.Twitch };
        TestPlatformConnection youTubeConnection = new() { TestPlatformSource = PlatformEventSource.YouTube };
        TestChatService chatService = new();
        ChatRepostService service = CreateService([twitchConnection, youTubeConnection], chatService);

        await service.StartAsync(CancellationToken.None);

        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.Twitch, "msg-1", "viewer123", "Hello everyone!");
        chatService.RaiseChatMessageReceived(chatEvent);

        await Task.Delay(100);

        Assert.Empty(twitchConnection.SentMessages);
        Assert.Single(youTubeConnection.SentMessages);
        Assert.Equal("[Twitch] viewer123: Hello everyone!", youTubeConnection.SentMessages[0]);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenChatMessageIsReceived_ThenItIsNotRepostedBackToOriginPlatform()
    {
        TestPlatformConnection twitchConnection = new() { TestPlatformSource = PlatformEventSource.Twitch };
        TestPlatformConnection youTubeConnection = new() { TestPlatformSource = PlatformEventSource.YouTube };
        TestChatService chatService = new();
        ChatRepostService service = CreateService([twitchConnection, youTubeConnection], chatService);

        await service.StartAsync(CancellationToken.None);

        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.Twitch, "msg-2", "viewer456", "Good morning!");
        chatService.RaiseChatMessageReceived(chatEvent);

        await Task.Delay(100);

        Assert.Empty(twitchConnection.SentMessages);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenRepostedMessageIsReceived_ThenItIsNotRepostedAgain()
    {
        TestPlatformConnection twitchConnection = new() { TestPlatformSource = PlatformEventSource.Twitch };
        TestPlatformConnection youTubeConnection = new() { TestPlatformSource = PlatformEventSource.YouTube };
        TestChatService chatService = new();
        ChatRepostService service = CreateService([twitchConnection, youTubeConnection], chatService);

        await service.StartAsync(CancellationToken.None);

        ChatEvent repostedEvent = CreateChatEvent(
            PlatformEventSource.Twitch,
            "msg-3",
            "viewer789",
            "[YouTube] SomeUser: This is already a repost");
        chatService.RaiseChatMessageReceived(repostedEvent);

        await Task.Delay(100);

        Assert.Empty(twitchConnection.SentMessages);
        Assert.Empty(youTubeConnection.SentMessages);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenEmptyMessageIsReceived_ThenItIsNotReposted()
    {
        TestPlatformConnection twitchConnection = new() { TestPlatformSource = PlatformEventSource.Twitch };
        TestPlatformConnection youTubeConnection = new() { TestPlatformSource = PlatformEventSource.YouTube };
        TestChatService chatService = new();
        ChatRepostService service = CreateService([twitchConnection, youTubeConnection], chatService);

        await service.StartAsync(CancellationToken.None);

        ChatEvent emptyEvent = CreateChatEvent(PlatformEventSource.Twitch, "msg-4", "viewer000", "");
        chatService.RaiseChatMessageReceived(emptyEvent);

        await Task.Delay(100);

        Assert.Empty(twitchConnection.SentMessages);
        Assert.Empty(youTubeConnection.SentMessages);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenSameMessageIsReceivedTwice_ThenItIsOnlyRepostedOnce()
    {
        TestPlatformConnection twitchConnection = new() { TestPlatformSource = PlatformEventSource.Twitch };
        TestPlatformConnection youTubeConnection = new() { TestPlatformSource = PlatformEventSource.YouTube };
        TestChatService chatService = new();
        ChatRepostService service = CreateService([twitchConnection, youTubeConnection], chatService);

        await service.StartAsync(CancellationToken.None);

        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.Twitch, "msg-5", "viewer555", "Duplicate test");
        chatService.RaiseChatMessageReceived(chatEvent);
        chatService.RaiseChatMessageReceived(chatEvent);

        await Task.Delay(100);

        Assert.Single(youTubeConnection.SentMessages);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenMultiplePlatformsAreConnected_ThenMessageIsRepostedToAllExceptOrigin()
    {
        TestPlatformConnection twitchConnection = new() { TestPlatformSource = PlatformEventSource.Twitch };
        TestPlatformConnection youTubeConnection = new() { TestPlatformSource = PlatformEventSource.YouTube };
        TestPlatformConnection discordConnection = new() { TestPlatformSource = PlatformEventSource.Discord };
        TestChatService chatService = new();
        ChatRepostService service = CreateService(
            [twitchConnection, youTubeConnection, discordConnection],
            chatService);

        await service.StartAsync(CancellationToken.None);

        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.YouTube, "msg-6", "ytuser", "Multi-platform test");
        chatService.RaiseChatMessageReceived(chatEvent);

        await Task.Delay(100);

        Assert.Empty(youTubeConnection.SentMessages);
        Assert.Single(twitchConnection.SentMessages);
        Assert.Single(discordConnection.SentMessages);
        Assert.Equal("[YouTube] ytuser: Multi-platform test", twitchConnection.SentMessages[0]);
        Assert.Equal("[YouTube] ytuser: Multi-platform test", discordConnection.SentMessages[0]);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenPlatformIsNotConnected_ThenMessageIsNotRepostedToIt()
    {
        TestPlatformConnection twitchConnection = new() { TestPlatformSource = PlatformEventSource.Twitch };
        TestPlatformConnection youTubeConnection = new()
        {
            TestPlatformSource = PlatformEventSource.YouTube,
            Connected = false
        };
        TestChatService chatService = new();
        ChatRepostService service = CreateService([twitchConnection, youTubeConnection], chatService);

        await service.StartAsync(CancellationToken.None);

        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.Twitch, "msg-7", "viewer999", "Test disconnected");
        chatService.RaiseChatMessageReceived(chatEvent);

        await Task.Delay(100);

        Assert.Empty(twitchConnection.SentMessages);
        Assert.Empty(youTubeConnection.SentMessages);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhenPlatformsSupportOutboundReposts_ThenMessagesOnlySkipUnsupportedTargets()
    {
        TestPlatformConnection twitchConnection = new() { TestPlatformSource = PlatformEventSource.Twitch };
        TestPlatformConnection youTubeConnection = new() { TestPlatformSource = PlatformEventSource.YouTube };
        TestPlatformConnection facebookConnection = new() { TestPlatformSource = PlatformEventSource.Facebook };
        TestPlatformConnection xConnection = new() { TestPlatformSource = PlatformEventSource.X };
        TestPlatformConnection linkedInConnection = new()
        {
            TestPlatformSource = PlatformEventSource.LinkedIn,
            SendException = new NotSupportedException("LinkedIn chat reposting is unavailable")
        };
        TestChatService chatService = new();
        ChatRepostService service = CreateService(
            [twitchConnection, youTubeConnection, facebookConnection, xConnection, linkedInConnection],
            chatService);

        await service.StartAsync(CancellationToken.None);

        ChatEvent chatEvent = CreateChatEvent(PlatformEventSource.Twitch, "msg-8", "viewer111", "Skip unsupported target");
        chatService.RaiseChatMessageReceived(chatEvent);

        await Task.Delay(100);

        Assert.Single(youTubeConnection.SentMessages);
        Assert.Single(facebookConnection.SentMessages);
        Assert.Single(xConnection.SentMessages);
        Assert.Equal(0, linkedInConnection.SendAttemptCount);

        await service.StopAsync(CancellationToken.None);
    }

    private static ChatRepostService CreateService(
        List<TestPlatformConnection> platformConnections,
        IChatService chatService)
    {
        return new ChatRepostService(
            platformConnections,
            chatService,
            NullLogger<ChatRepostService>.Instance);
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
            Channel = "test-channel",
            ExternalId = externalId,
            Content = content,
            OccurredAt = DateTime.UtcNow
        };
    }

    private sealed class TestChatService : IChatService
    {
        public bool Connected { get; set; } = true;

        public event EventHandler<ChatEvent>? OnChatMessageRecieved;
#pragma warning disable CS0067
        public event EventHandler<PlatformEvent>? OnPlatformEventReceived;
#pragma warning restore CS0067

        public void RaiseChatMessageReceived(ChatEvent chatEvent)
        {
            OnChatMessageRecieved?.Invoke(this, chatEvent);
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Connect(CancellationToken ct)
        {
            Connected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken ct)
        {
            Connected = false;
            return Task.CompletedTask;
        }
    }

    private sealed class TestPlatformConnection : IPlatformConnection
    {
        public PlatformEventSource TestPlatformSource { get; init; } = PlatformEventSource.Null;

        public List<string> SentMessages { get; } = [];

        public int SendAttemptCount { get; private set; }

        public string PlatformName { get; init; } = "Test";

        public PlatformConnectionState State => Connected
            ? PlatformConnectionState.Connected
            : PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool Connected { get; set; } = true;

        public Exception? SendException { get; init; }

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
            Connected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            Connected = false;
            return Task.CompletedTask;
        }

        public string GetAuthorizationUrl()
        {
            return string.Empty;
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            SendAttemptCount++;

            if (SendException is not null)
            {
                throw SendException;
            }

            SentMessages.Add(message);
            return Task.CompletedTask;
        }

        public override string ToString()
        {
            return TestPlatformSource switch
            {
                PlatformEventSource.Twitch => "TwitchService",
                PlatformEventSource.YouTube => "YouTubeService",
                PlatformEventSource.Discord => "DiscordService",
                PlatformEventSource.Facebook => "FacebookService",
                PlatformEventSource.X => "XService",
                PlatformEventSource.LinkedIn => "LinkedInService",
                PlatformEventSource.TikTok => "TikTokService",
                _ => "NullPlatformConnection"
            };
        }
    }
}