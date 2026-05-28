using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;
using PersistedPlatformEvent = Thiccdal.Data.Models.PlatformEvent;
using RuntimePlatformEventSource = Thiccdal.Infrastructure.Bot.Models.PlatformEventSource;
using RuntimePlatformEventType = Thiccdal.Infrastructure.Bot.Models.PlatformEventType;

namespace Thiccdal.Data.Tests;

public sealed class ChatMessagePersistenceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenPersistingChatMessage_ThenItLinksPlatformEventAndPlatformUser()
    {
        await using ApplicationDbContext context = await CreateDbContextAsync();

        var platformUser = new PlatformUser
        {
            Source = RuntimePlatformEventSource.Twitch,
            PlatformUserId = "viewer-1",
            DisplayName = "viewer"
        };
        var platformEvent = new PersistedPlatformEvent
        {
            Source = RuntimePlatformEventSource.Twitch,
            Type = RuntimePlatformEventType.ChatMessage,
            ExternalId = "message-1",
            Author = "viewer",
            Channel = "thindal",
            Summary = "hello",
            Content = "hello",
            RawData = "{\"message\":\"hello\"}",
            OccurredAt = DateTime.UtcNow
        };

        context.ChatMessages.Add(new ChatMessage
        {
            PlatformEvent = platformEvent,
            PlatformUser = platformUser,
            Source = RuntimePlatformEventSource.Twitch,
            Content = "hello",
            RawData = "{\"message\":\"hello\"}",
            SentAt = platformEvent.OccurredAt
        });

        await context.SaveChangesAsync();

        ChatMessage storedMessage = await context.ChatMessages
            .Include(chatMessage => chatMessage.PlatformEvent)
            .Include(chatMessage => chatMessage.PlatformUser)
            .SingleAsync();

        Assert.Equal("message-1", storedMessage.PlatformEvent.ExternalId);
        Assert.Equal("viewer-1", storedMessage.PlatformUser.PlatformUserId);
        Assert.Equal("hello", storedMessage.Content);
    }
}
