using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Remotes;
using PersistedPlatformEvent = Thiccdal.Data.Models.PlatformEvent;
using RuntimeChatEvent = Thiccdal.Infrastructure.Bot.Models.ChatEvent;
using RuntimeMembershipEvent = Thiccdal.Infrastructure.Bot.Models.MembershipEvent;
using RuntimePlatformEvent = Thiccdal.Infrastructure.Bot.Models.PlatformEvent;
using RuntimePlatformEventSource = Thiccdal.Infrastructure.Bot.Models.PlatformEventSource;
using RuntimePlatformEventType = Thiccdal.Infrastructure.Bot.Models.PlatformEventType;
using RuntimeRaidEvent = Thiccdal.Infrastructure.Bot.Models.TwitchRaidEvent;
using RuntimeRedeemEvent = Thiccdal.Infrastructure.Bot.Models.TwitchRedeemEvent;
using RuntimeSuperChatEvent = Thiccdal.Infrastructure.Bot.Models.SuperChatEvent;
using RuntimeSubscribeEvent = Thiccdal.Infrastructure.Bot.Models.TwitchSubscribeEvent;

namespace Thiccdal.Data.Tests;

public sealed class EventPersistenceServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenPersistingPlatformEvent_ThenRecordIsStoredAndIdAssigned()
    {
        EventPersistenceService persistenceService = CreatePersistenceService();
        RuntimePlatformEvent platformEvent = new()
        {
            Source = RuntimePlatformEventSource.Null,
            Type = RuntimePlatformEventType.Raw,
            SourceEventType = "null.raw",
            Author = "system",
            Channel = "offline",
            ExternalId = "raw-1",
            Summary = "Null platform emitted a raw event",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"kind\":\"raw\"}"
        };

        await persistenceService.Persist(platformEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        PersistedPlatformEvent storedEvent = await dbContext.PlatformEvents.SingleAsync();
        Assert.True(platformEvent.PersistedRecordId > 0);
        Assert.Equal(platformEvent.PersistedRecordId, storedEvent.Id);
        Assert.Equal(RuntimePlatformEventSource.Null, storedEvent.Source);
        Assert.Equal(RuntimePlatformEventType.Raw, storedEvent.Type);
        Assert.Equal("null.raw", storedEvent.SourceEventType);
        Assert.Equal("{\"kind\":\"raw\"}", storedEvent.RawData);
    }

    [Fact]
    public async Task WhenPersistingChatEvent_ThenPlatformUserAndChatMessageAreStored()
    {
        EventPersistenceService persistenceService = CreatePersistenceService();
        RuntimeChatEvent chatEvent = new()
        {
            Source = RuntimePlatformEventSource.Twitch,
            Type = RuntimePlatformEventType.ChatMessage,
            SourceEventType = "channel.chat.message",
            Author = "viewer",
            Channel = "thindal",
            ExternalId = "message-1",
            Summary = "viewer said hello",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"payload\":{\"event\":{\"chatter_user_id\":\"viewer-42\"}}}",
            Content = "hello",
            HtmlContent = "<span>hello</span>"
        };

        await persistenceService.Persist(chatEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        PlatformUser platformUser = await dbContext.PlatformUsers.SingleAsync();
        ChatMessage storedMessage = await dbContext.ChatMessages
            .Include(chatMessage => chatMessage.PlatformEvent)
            .Include(chatMessage => chatMessage.PlatformUser)
            .SingleAsync();

        Assert.True(chatEvent.PersistedRecordId > 0);
        Assert.Equal("viewer-42", platformUser.PlatformUserId);
        Assert.Equal("viewer", platformUser.DisplayName);
        Assert.Equal("channel.chat.message", storedMessage.PlatformEvent.SourceEventType);
        Assert.Equal(chatEvent.PersistedRecordId, storedMessage.PlatformEventId);
        Assert.Equal(platformUser.Id, storedMessage.PlatformUserId);
        Assert.Equal("hello", storedMessage.Content);
        Assert.Equal("<span>hello</span>", storedMessage.HtmlContent);
    }

    [Fact]
    public async Task WhenSubscribeEventPersisted_ThenTierAndIsGiftAndGifterAreStored()
    {
        EventPersistenceService persistenceService = CreatePersistenceService();
        RuntimeSubscribeEvent subscribeEvent = new()
        {
            Source = RuntimePlatformEventSource.Twitch,
            Type = RuntimePlatformEventType.Subscribe,
            Author = "viewer",
            Channel = "thindal",
            ExternalId = "sub-1",
            Summary = "viewer received a gifted Tier 1000 sub",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"subscription\":true}",
            Tier = "1000",
            IsGift = true,
            GifterUserId = "gifter-42"
        };

        await persistenceService.Persist(subscribeEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        SubscribeEvent storedEvent = await dbContext.PlatformEvents.OfType<SubscribeEvent>().SingleAsync();
        PlatformUser storedGifter = await dbContext.PlatformUsers.SingleAsync();
        Assert.Equal("1000", storedEvent.Tier);
        Assert.True(storedEvent.IsGift);
        Assert.Equal(storedGifter.Id, storedEvent.GifterPlatformUserId);
        Assert.Equal("gifter-42", storedGifter.PlatformUserId);
    }

    [Fact]
    public async Task WhenRedeemEventPersisted_ThenRewardTitleAndUserInputAreStored()
    {
        EventPersistenceService persistenceService = CreatePersistenceService();
        RuntimeRedeemEvent redeemEvent = new()
        {
            Source = RuntimePlatformEventSource.Twitch,
            Type = RuntimePlatformEventType.Redeem,
            Author = "viewer",
            Channel = "thindal",
            ExternalId = "redeem-1",
            Summary = "viewer redeemed Ask a question",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"reward\":true}",
            RewardId = "reward-1",
            RewardTitle = "Ask a question",
            UserInput = "How are you?"
        };

        await persistenceService.Persist(redeemEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        RedeemEvent storedEvent = await dbContext.PlatformEvents.OfType<RedeemEvent>().SingleAsync();
        Assert.Equal("Ask a question", storedEvent.RewardTitle);
        Assert.Equal("How are you?", storedEvent.UserInput);
    }

    [Fact]
    public async Task WhenRaidEventPersisted_ThenRaidingChannelAndViewerCountAreStored()
    {
        EventPersistenceService persistenceService = CreatePersistenceService();
        RuntimeRaidEvent raidEvent = new()
        {
            Source = RuntimePlatformEventSource.Twitch,
            Type = RuntimePlatformEventType.Raid,
            Author = "raider",
            Channel = "thindal",
            ExternalId = "raid-1",
            Summary = "raider raided with 12 viewers",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"raid\":true}",
            RaidingChannel = "raiderchannel",
            ViewerCount = 12
        };

        await persistenceService.Persist(raidEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        RaidEvent storedEvent = await dbContext.PlatformEvents.OfType<RaidEvent>().SingleAsync();
        Assert.Equal("raiderchannel", storedEvent.RaidingChannel);
        Assert.Equal(12, storedEvent.ViewerCount);
    }

    [Fact]
    public async Task WhenSuperChatEventPersisted_ThenTypedFieldsAreStored()
    {
        EventPersistenceService persistenceService = CreatePersistenceService();
        RuntimeSuperChatEvent superChatEvent = new()
        {
            Source = RuntimePlatformEventSource.YouTube,
            Type = RuntimePlatformEventType.SuperChat,
            SourceEventType = "superChatEvent",
            Author = "viewer",
            Channel = "channel-1",
            ExternalId = "sc-1",
            Summary = "viewer sent $5.00",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"type\":\"superChatEvent\"}",
            AmountMicros = 5_000_000,
            Currency = "USD",
            DisplayString = "$5.00",
            UserComment = "Great stream!"
        };

        await persistenceService.Persist(superChatEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        SuperChatEvent storedEvent = await dbContext.PlatformEvents.OfType<SuperChatEvent>().SingleAsync();
        Assert.Equal(5_000_000, storedEvent.AmountMicros);
        Assert.Equal("USD", storedEvent.Currency);
        Assert.Equal("$5.00", storedEvent.DisplayString);
        Assert.Equal("Great stream!", storedEvent.UserComment);
        Assert.Equal("superChatEvent", storedEvent.SourceEventType);
    }

    [Fact]
    public async Task WhenMembershipEventPersisted_ThenTypedFieldsAreStored()
    {
        EventPersistenceService persistenceService = CreatePersistenceService();
        RuntimeMembershipEvent membershipEvent = new()
        {
            Source = RuntimePlatformEventSource.YouTube,
            Type = RuntimePlatformEventType.Membership,
            SourceEventType = "memberMilestoneChatEvent",
            Author = "viewer",
            Channel = "channel-1",
            ExternalId = "member-1",
            Summary = "viewer membership milestone",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"type\":\"memberMilestoneChatEvent\"}",
            LevelName = "Gold",
            MonthCount = 6
        };

        await persistenceService.Persist(membershipEvent);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        MembershipEvent storedEvent = await dbContext.PlatformEvents.OfType<MembershipEvent>().SingleAsync();
        Assert.Equal("Gold", storedEvent.LevelName);
        Assert.Equal(6, storedEvent.MonthCount);
        Assert.Equal("memberMilestoneChatEvent", storedEvent.SourceEventType);
    }

    private EventPersistenceService CreatePersistenceService()
    {
        ServiceCollection services = new();
        services.AddSingleton(new ChatPersistenceService(DbContextFactory, new NullLogger<ChatPersistenceService>()));
        services.AddScoped<IChatPersistenceService>(serviceProvider => serviceProvider.GetRequiredService<ChatPersistenceService>());
        ServiceProvider provider = services.BuildServiceProvider();

        return new EventPersistenceService(
            DbContextFactory,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new NullLogger<EventPersistenceService>());
    }
}
