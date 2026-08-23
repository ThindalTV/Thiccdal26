using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Data.Models;
using RuntimeChatEvent = Thiccdal.Infrastructure.Bot.Models.ChatEvent;
using RuntimePlatformEventSource = Thiccdal.Infrastructure.Bot.Models.PlatformEventSource;
using RuntimePlatformEventType = Thiccdal.Infrastructure.Bot.Models.PlatformEventType;

namespace Thiccdal.Data.Tests;

public sealed class ChatPersistenceServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenPersistingChatEvent_ThenPlatformEventChatMessageAndUserAreStored()
    {
        ChatPersistenceService persistenceService = new(
            DbContextFactory,
            new NullLogger<ChatPersistenceService>());
        RuntimeChatEvent chatEvent = new RuntimeChatEvent
        {
            Source = RuntimePlatformEventSource.Null,
            Type = RuntimePlatformEventType.ChatMessage,
            SourceEventType = "textMessageEvent",
            Author = "viewer",
            Channel = "thiccdal",
            ExternalId = "message-1",
            Summary = "viewer said hello",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"id\":\"message-1\",\"snippet\":{\"type\":\"textMessageEvent\",\"publishedAt\":\"2026-06-01T10:00:00Z\",\"textMessageDetails\":{\"messageText\":\"hello\"}},\"authorDetails\":{\"channelId\":\"viewer-42\",\"displayName\":\"viewer\"}}",
            Content = "hello",
            HtmlContent = "<span>hello</span>"
        };

        await persistenceService.Persist(chatEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        PlatformEvent storedEvent = await dbContext.PlatformEvents.SingleAsync();
        ChatMessage storedMessage = await dbContext.ChatMessages
            .Include(chatMessage => chatMessage.PlatformEvent)
            .Include(chatMessage => chatMessage.PlatformUser)
            .SingleAsync();
        PlatformUser storedUser = await dbContext.PlatformUsers.SingleAsync();

        Assert.True(chatEvent.PersistedRecordId > 0);
        Assert.Equal(chatEvent.PersistedRecordId, storedEvent.Id);
        Assert.Equal(storedEvent.Id, storedMessage.PlatformEventId);
        Assert.Equal(storedUser.Id, storedMessage.PlatformUserId);
        Assert.Equal("viewer-42", storedUser.PlatformUserId);
        Assert.Equal("viewer", storedUser.DisplayName);
        Assert.Equal("textMessageEvent", storedEvent.SourceEventType);
        Assert.Equal("hello", storedMessage.Content);
    }

    [Fact]
    public async Task WhenChatEventIsAlreadyPersisted_ThenChatPersistenceSkipsDuplicateWrites()
    {
        ChatPersistenceService persistenceService = new(
            DbContextFactory,
            new NullLogger<ChatPersistenceService>());
        RuntimeChatEvent chatEvent = new RuntimeChatEvent
        {
            PersistedRecordId = 99,
            Source = RuntimePlatformEventSource.Twitch,
            Type = RuntimePlatformEventType.ChatMessage,
            Author = "viewer",
            Channel = "thiccdal",
            ExternalId = "message-2",
            Summary = "already persisted",
            OccurredAt = DateTime.UtcNow,
            RawData = "{}",
            Content = "hello"
        };

        await persistenceService.Persist(chatEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        Assert.Empty(await dbContext.PlatformEvents.ToListAsync());
        Assert.Empty(await dbContext.ChatMessages.ToListAsync());
        Assert.Empty(await dbContext.PlatformUsers.ToListAsync());
    }

    [Fact]
    public async Task WhenPersistingNonTwitchChatEvent_ThenAuthorIdIsStoredAsPlatformUserId()
    {
        ChatPersistenceService persistenceService = new(
            DbContextFactory,
            new NullLogger<ChatPersistenceService>());
        RuntimeChatEvent chatEvent = new()
        {
            Source = RuntimePlatformEventSource.Null,
            Type = RuntimePlatformEventType.ChatMessage,
            SourceEventType = "facebook.comment",
            Author = "viewer",
            Channel = "live-42",
            ExternalId = "comment-42",
            Summary = "viewer said hello",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"id\":\"comment-42\",\"from\":{\"id\":\"psid-42\",\"name\":\"viewer\"},\"message\":\"hello\"}",
            Content = "hello"
        };

        await persistenceService.Persist(chatEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        PlatformUser storedUser = await dbContext.PlatformUsers.SingleAsync();

        Assert.Equal("psid-42", storedUser.PlatformUserId);
    }

    [Fact]
    public async Task WhenPlatformUserHasCanonicalIdentity_ThenChatEventPrefersCanonicalDisplayName()
    {
        await using (ApplicationDbContext seedContext = await CreateDbContextAsync())
        {
            UserIdentity identity = new()
            {
                DisplayName = "Kaylee Prime"
            };
            seedContext.PlatformUsers.Add(
                new PlatformUser
                {
                    Source = RuntimePlatformEventSource.Twitch,
                    PlatformUserId = "viewer-42",
                    DisplayName = "KayleeRaw",
                    UserIdentity = identity
                });
            await seedContext.SaveChangesAsync();
        }

        ChatPersistenceService persistenceService = new(
            DbContextFactory,
            new NullLogger<ChatPersistenceService>());
        RuntimeChatEvent chatEvent = new()
        {
            Source = RuntimePlatformEventSource.Twitch,
            Type = RuntimePlatformEventType.ChatMessage,
            SourceEventType = "channel.chat.message",
            Author = "KayleeRaw",
            PlatformUserId = "viewer-42",
            Channel = "thiccdal",
            ExternalId = "message-canonical-1",
            Summary = "KayleeRaw said hello",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"event\":{\"chatter_user_id\":\"viewer-42\"}}",
            Content = "hello"
        };

        await persistenceService.Persist(chatEvent);

        Assert.Equal("Kaylee Prime", chatEvent.PreferredAuthor);
    }

    [Fact]
    public async Task WhenPersistingNonTwitchChatEvent_ThenAdapterUserIdAndChannelIdArePreserved()
    {
        ChatPersistenceService persistenceService = new(
            DbContextFactory,
            new NullLogger<ChatPersistenceService>());
        RuntimeChatEvent chatEvent = new()
        {
            Source = RuntimePlatformEventSource.Null,
            Type = RuntimePlatformEventType.ChatMessage,
            SourceEventType = "discord.message",
            Author = "viewer",
            Channel = "987654321",
            ExternalId = "message-42",
            Summary = "viewer said hello",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"payload\":{\"event\":{\"user_id\":\"discord-user-42\",\"channel_id\":\"987654321\"}},\"author\":{\"id\":\"discord-user-42\",\"username\":\"viewer\"}}",
            Content = "hello"
        };

        await persistenceService.Persist(chatEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        PlatformUser storedUser = await dbContext.PlatformUsers.SingleAsync();
        PlatformEvent storedEvent = await dbContext.PlatformEvents.SingleAsync();

        Assert.Equal("discord-user-42", storedUser.PlatformUserId);
        Assert.Equal("987654321", storedEvent.Channel);
    }
}
