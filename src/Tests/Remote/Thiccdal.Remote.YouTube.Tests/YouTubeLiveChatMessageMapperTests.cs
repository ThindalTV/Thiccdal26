using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Remote.YouTube;

namespace Thiccdal.Remote.YouTube.Tests;

public sealed class YouTubeLiveChatMessageMapperTests
{
    private readonly YouTubeLiveChatMessageMapper _mapper = new(NullLogger<YouTubeLiveChatMessageMapper>.Instance);

    [Fact]
    public void WhenTextMessageEvent_ThenChatMessageWithCorrectContent()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateTextMessageItem()),
            "my-channel-id");

        ChatEvent chatEvent = Assert.IsType<ChatEvent>(Assert.Single(events));
        Assert.Equal("Hello YouTube!", chatEvent.Content);
        Assert.Equal("TestUser", chatEvent.Author);
        Assert.Equal(PlatformEventType.ChatMessage, chatEvent.Type);
        Assert.Equal("textMessageEvent", chatEvent.SourceEventType);
    }

    [Fact]
    public void WhenTextMessageEvent_ThenSentAtUsesPublishedAt()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateTextMessageItem(publishedAt: "2026-06-01T10:00:00Z")),
            "my-channel-id");

        ChatEvent chatEvent = Assert.IsType<ChatEvent>(Assert.Single(events));
        Assert.Equal(new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), chatEvent.OccurredAt);
    }

    [Fact]
    public void WhenTextMessageEvent_ThenChannelIdIsUsedAsPlatformUserId()
    {
        string itemJson = YouTubeTestData.CreateTextMessageItem(authorChannelId: "author-ch-42");

        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(itemJson),
            "my-channel-id");

        ChatEvent chatEvent = Assert.IsType<ChatEvent>(Assert.Single(events));
        Assert.Equal("author-ch-42", chatEvent.PlatformUserId);
    }

    [Fact]
    public void WhenTextMessageEvent_ThenDisplayNameIsPreserved()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateTextMessageItem(displayName: "DisplayName")),
            "my-channel-id");

        ChatEvent chatEvent = Assert.IsType<ChatEvent>(Assert.Single(events));
        Assert.Equal("DisplayName", chatEvent.Author);
        Assert.Equal("DisplayName: Hello YouTube!", chatEvent.Summary);
    }

    [Fact]
    public void WhenSuperChatEvent_ThenSuperChatEventWithCorrectAmountAndCurrency()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateSuperChatItem()),
            "my-channel-id");

        SuperChatEvent superChatEvent = Assert.IsType<SuperChatEvent>(Assert.Single(events));
        Assert.Equal(5_000_000, superChatEvent.AmountMicros);
        Assert.Equal("USD", superChatEvent.Currency);
        Assert.Equal("superChatEvent", superChatEvent.SourceEventType);
    }

    [Fact]
    public void WhenSuperChatEvent_ThenDisplayStringIsPreserved()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateSuperChatItem(amountDisplayString: "$5.00")),
            "my-channel-id");

        SuperChatEvent superChatEvent = Assert.IsType<SuperChatEvent>(Assert.Single(events));
        Assert.Equal("$5.00", superChatEvent.DisplayString);
    }

    [Fact]
    public void WhenSuperChatEventHasComment_ThenUserCommentIsSet()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateSuperChatItem(comment: "Great stream!")),
            "my-channel-id");

        SuperChatEvent superChatEvent = Assert.IsType<SuperChatEvent>(Assert.Single(events));
        Assert.Equal("Great stream!", superChatEvent.UserComment);
    }

    [Fact]
    public void WhenSuperChatEventNoComment_ThenUserCommentIsNull()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateSuperChatItem(comment: null)),
            "my-channel-id");

        SuperChatEvent superChatEvent = Assert.IsType<SuperChatEvent>(Assert.Single(events));
        Assert.Null(superChatEvent.UserComment);
    }

    [Fact]
    public void WhenMemberMilestoneChatEvent_ThenMembershipEventWithMonthCount()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateMembershipItem()),
            "my-channel-id");

        MembershipEvent membershipEvent = Assert.IsType<MembershipEvent>(Assert.Single(events));
        Assert.Equal(6, membershipEvent.MonthCount);
        Assert.Equal("Gold", membershipEvent.LevelName);
        Assert.Equal("memberMilestoneChatEvent", membershipEvent.SourceEventType);
    }

    [Fact]
    public void WhenNewSponsorEvent_ThenMembershipEventWithNullMonthCount()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateNewSponsorItem()),
            "my-channel-id");

        MembershipEvent membershipEvent = Assert.IsType<MembershipEvent>(Assert.Single(events));
        Assert.Null(membershipEvent.MonthCount);
        Assert.Equal("newSponsorEvent", membershipEvent.SourceEventType);
    }

    [Fact]
    public void WhenNewSponsorEvent_ThenIsNewMemberIsTrue()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateNewSponsorItem()),
            "my-channel-id");

        MembershipEvent membershipEvent = Assert.IsType<MembershipEvent>(Assert.Single(events));
        Assert.True(membershipEvent.IsNewMember);
    }

    [Fact]
    public void WhenUnknownSnippetType_ThenBasePlatformEventWithRawDataJson()
    {
        string itemJson = YouTubeTestData.CreateUnknownItem();

        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(itemJson),
            "my-channel-id");

        RawEvent rawEvent = Assert.IsType<RawEvent>(Assert.Single(events));
        Assert.Equal(itemJson, rawEvent.RawData);
    }

    [Fact]
    public void WhenUnknownSnippetType_ThenEventTypeMatchesSnippetType()
    {
        IReadOnlyList<PlatformEvent> events = _mapper.MapMessages(
            YouTubeTestData.CreatePollPayload(YouTubeTestData.CreateUnknownItem(type: "futureFeatureEvent")),
            "my-channel-id");

        RawEvent rawEvent = Assert.IsType<RawEvent>(Assert.Single(events));
        Assert.Equal(PlatformEventType.Raw, rawEvent.Type);
        Assert.Equal("futureFeatureEvent", rawEvent.SourceEventType);
        Assert.Contains("futureFeatureEvent", rawEvent.Summary, StringComparison.Ordinal);
    }
}
