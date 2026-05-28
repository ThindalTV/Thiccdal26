using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Remote.Facebook;

namespace Thiccdal.Remote.Facebook.Tests;

public sealed class FacebookEventMapperTests
{
    [Fact]
    public void WhenFacebookComment_ThenChatEventWithCorrectContent()
    {
        ChatEvent chatEvent = FacebookEventMapper.ToChatEvent(
            FacebookTestData.CreateComment(
                id: "comment-1",
                message: "Hello!",
                userId: "psid-1",
                displayName: "Viewer Name",
                createdTime: "2024-06-01T14:05:00+0000"),
            "live-1");

        Assert.Equal(PlatformEventSource.Facebook, chatEvent.Source);
        Assert.Equal(PlatformEventType.ChatMessage, chatEvent.Type);
        Assert.Equal("Hello!", chatEvent.Content);
        Assert.Equal("Viewer Name", chatEvent.Author);
        Assert.Equal("live-1", chatEvent.Channel);
        Assert.Equal("comment-1", chatEvent.ExternalId);
        Assert.Contains("\"user_id\":\"psid-1\"", chatEvent.RawData, StringComparison.Ordinal);
    }

    [Fact]
    public void WhenFacebookComment_ThenCreatedTimeIsUsedForOccurredAt()
    {
        ChatEvent chatEvent = FacebookEventMapper.ToChatEvent(
            FacebookTestData.CreateComment(
                id: "comment-1",
                message: "Hello!",
                userId: "psid-1",
                displayName: "Viewer Name",
                createdTime: "2024-06-01T14:05:00+0000"),
            "live-1");

        Assert.Equal(new DateTime(2024, 6, 1, 14, 5, 0, DateTimeKind.Utc), chatEvent.OccurredAt);
    }

    [Theory]
    [InlineData("LIKE")]
    [InlineData("LOVE")]
    public void WhenKnownReaction_ThenReactionEventWithCorrectEmoteName(string reactionType)
    {
        PlatformEvent platformEvent = FacebookEventMapper.ToReactionEvent(
            FacebookTestData.CreateReaction(
                id: "reaction-1",
                type: reactionType,
                name: "Viewer Name"),
            "live-1");

        ReactionEvent reactionEvent = Assert.IsType<ReactionEvent>(platformEvent);
        Assert.Equal(reactionType, reactionEvent.EmoteName);
        Assert.Equal("live-1", reactionEvent.MessageId);
        Assert.Equal(PlatformEventType.Raw, reactionEvent.Type);
    }

    [Fact]
    public void WhenUnknownReactionType_ThenBasePlatformEventWithRawData()
    {
        PlatformEvent platformEvent = FacebookEventMapper.ToReactionEvent(
            FacebookTestData.CreateReaction(
                id: "reaction-1",
                type: "CARE",
                name: "Viewer Name"),
            "live-1");

        RawEvent rawEvent = Assert.IsType<RawEvent>(platformEvent);
        Assert.Equal(PlatformEventSource.Facebook, rawEvent.Source);
        Assert.Equal(PlatformEventType.Raw, rawEvent.Type);
        Assert.Contains("\"type\":\"CARE\"", rawEvent.RawData);
    }
}
