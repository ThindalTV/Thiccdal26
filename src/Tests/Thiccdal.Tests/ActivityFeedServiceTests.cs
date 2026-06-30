using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class ActivityFeedServiceTests
{
    [Fact]
    public async Task WhenChatEventContainsRichParts_ThenFeedEntryContainsRenderedHtml()
    {
        TestChatService chatService = new();
        QuestionOverlayService questionOverlayService = new();
        questionOverlayService.ClearWaitingQuestions();
        ActivityFeedService activityFeedService = new(
            chatService,
            new QuestionLocatorService(new StubQuestionDetectionService(false)),
            questionOverlayService,
            Options.Create(new ChatBotOptions()),
            NullLogger<ActivityFeedService>.Instance);
        await activityFeedService.StartAsync(CancellationToken.None);

        chatService.Publish(
            new ChatEvent
            {
                Source = PlatformEventSource.Twitch,
                Type = PlatformEventType.ChatMessage,
                Author = "Kaylee",
                Channel = "thiccdal",
                Summary = "Kappa Cheer100",
                Content = "Kappa Cheer100",
                Color = "#a970ff",
                HtmlContent = string.Empty,
                Badges = [new ChatBadge("subscriber", "12", "subscriber")],
                Parts =
                [
                    new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Emote,
                        Text = "Kappa",
                        AssetUrl = "https://example.com/kappa.png"
                    },
                    new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Text,
                        Text = " "
                    },
                    new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Cheer,
                        Text = "Cheer100",
                        Amount = 100
                    }
                ]
            });

        ActivityFeedEntry entry = Assert.Single(activityFeedService.GetEntries());

        Assert.Equal("Kaylee", entry.Sender);
        Assert.Equal("#a970ff", entry.SenderColor);
        Assert.Contains("chat-badge", entry.HtmlContent);
        Assert.Contains("chat-inline-emote", entry.HtmlContent);
        Assert.Contains("chat-inline-cheer", entry.HtmlContent);
    }

    [Fact]
    public async Task WhenChatEventHasCanonicalAuthor_ThenFeedEntryUsesCanonicalSender()
    {
        TestChatService chatService = new();
        QuestionOverlayService questionOverlayService = new();
        questionOverlayService.ClearWaitingQuestions();
        ActivityFeedService activityFeedService = new(
            chatService,
            new QuestionLocatorService(new StubQuestionDetectionService(false)),
            questionOverlayService,
            Options.Create(new ChatBotOptions()),
            NullLogger<ActivityFeedService>.Instance);
        await activityFeedService.StartAsync(CancellationToken.None);

        chatService.Publish(
            new ChatEvent
            {
                Source = PlatformEventSource.Twitch,
                Type = PlatformEventType.ChatMessage,
                Author = "KayleeRaw",
                PreferredAuthor = "Kaylee Prime",
                Channel = "thiccdal",
                Summary = "KayleeRaw said hello",
                Content = "hello"
            });

        ActivityFeedEntry entry = Assert.Single(activityFeedService.GetEntries());

        Assert.Equal("Kaylee Prime", entry.Sender);
    }

    [Fact]
    public async Task WhenAudienceEventsArrive_ThenFeedKeepsNewestEntriesFirst()
    {
        TestChatService chatService = new();
        QuestionOverlayService questionOverlayService = new();
        questionOverlayService.ClearWaitingQuestions();
        ActivityFeedService activityFeedService = new(
            chatService,
            new QuestionLocatorService(new StubQuestionDetectionService(false)),
            questionOverlayService,
            Options.Create(new ChatBotOptions()),
            NullLogger<ActivityFeedService>.Instance);
        await activityFeedService.StartAsync(CancellationToken.None);

        chatService.Publish(
            new TwitchFollowEvent
            {
                Source = PlatformEventSource.Twitch,
                Type = PlatformEventType.Follow,
                Author = "viewer1",
                Channel = "thiccdal",
                Summary = "viewer1 followed thiccdal"
            });
        chatService.Publish(
            new TwitchCheerEvent
            {
                Source = PlatformEventSource.Twitch,
                Type = PlatformEventType.Cheer,
                Author = "viewer2",
                Channel = "thiccdal",
                Summary = "viewer2 cheered 250 bits",
                Bits = 250,
                Message = "Let's go"
            });
        chatService.Publish(
            new TwitchRaidEvent
            {
                Source = PlatformEventSource.Twitch,
                Type = PlatformEventType.Raid,
                Author = "raider",
                Channel = "thiccdal",
                Summary = "raider raided thiccdal with 42 viewers",
                ViewerCount = 42
            });

        IReadOnlyList<ActivityFeedEntry> entries = activityFeedService.GetEntries();

        Assert.Equal(3, entries.Count);
        Assert.Equal(PlatformEventType.Raid, entries[0].Type);
        Assert.Equal(PlatformEventType.Cheer, entries[1].Type);
        Assert.Equal(PlatformEventType.Follow, entries[2].Type);
    }

    [Fact]
    public async Task WhenDetectorAcceptsChatMessage_ThenQuestionIsQueued()
    {
        TestChatService chatService = new();
        QuestionOverlayService questionOverlayService = new();
        questionOverlayService.ClearWaitingQuestions();
        ActivityFeedService activityFeedService = new(
            chatService,
            new QuestionLocatorService(new StubQuestionDetectionService(true)),
            questionOverlayService,
            Options.Create(new ChatBotOptions()),
            NullLogger<ActivityFeedService>.Instance);
        await activityFeedService.StartAsync(CancellationToken.None);

        chatService.Publish(
            new ChatEvent
            {
                Source = PlatformEventSource.Twitch,
                Type = PlatformEventType.ChatMessage,
                Author = "Viewer",
                Channel = "thiccdal",
                Summary = "?Can you show that again Kappa",
                Content = "?Can you show that again Kappa",
                Parts =
                [
                    new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Text,
                        Text = "?Can you show that again "
                    },
                    new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Emote,
                        Text = "Kappa",
                        AssetUrl = "https://example.com/kappa.png"
                    }
                ]
            });

        QuestionDashboardState state = await WaitForState(
            questionOverlayService,
            expectedWaitingQuestions: 1,
            CancellationToken.None);
        QuestionQueueItem queuedQuestion = Assert.Single(state.WaitingQuestions);
        Assert.Equal("?Can you show that again Kappa", queuedQuestion.Text);
        Assert.Equal("TWITCH", queuedQuestion.Platform);
    }

    [Fact]
    public async Task WhenDetectorRejectsChatMessage_ThenQuestionIsNotQueued()
    {
        TestChatService chatService = new();
        QuestionOverlayService questionOverlayService = new();
        questionOverlayService.ClearWaitingQuestions();
        ActivityFeedService activityFeedService = new(
            chatService,
            new QuestionLocatorService(new StubQuestionDetectionService(false)),
            questionOverlayService,
            Options.Create(new ChatBotOptions()),
            NullLogger<ActivityFeedService>.Instance);
        await activityFeedService.StartAsync(CancellationToken.None);

        chatService.Publish(
            new ChatEvent
            {
                Source = PlatformEventSource.Twitch,
                Type = PlatformEventType.ChatMessage,
                Author = "Viewer",
                Channel = "thiccdal",
                Summary = "That was awesome",
                Content = "That was awesome"
            });

        await Task.Delay(150);

        Assert.Empty(questionOverlayService.GetState().WaitingQuestions);
    }

    [Fact]
    public async Task WhenStopCalledConcurrently_ThenItDoesNotThrow()
    {
        TestChatService chatService = new();
        QuestionOverlayService questionOverlayService = new();
        questionOverlayService.ClearWaitingQuestions();
        ActivityFeedService activityFeedService = new(
            chatService,
            new QuestionLocatorService(new StubQuestionDetectionService(false)),
            questionOverlayService,
            Options.Create(new ChatBotOptions()),
            NullLogger<ActivityFeedService>.Instance);
        await activityFeedService.StartAsync(CancellationToken.None);

        await Task.WhenAll(
            activityFeedService.StopAsync(CancellationToken.None),
            activityFeedService.StopAsync(CancellationToken.None));
    }

    private static async Task<QuestionDashboardState> WaitForState(
        QuestionOverlayService questionOverlayService,
        int expectedWaitingQuestions,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(2);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            QuestionDashboardState state = questionOverlayService.GetState();
            if (state.WaitingQuestions.Count == expectedWaitingQuestions)
            {
                return state;
            }

            await Task.Delay(50, cancellationToken);
        }

        return questionOverlayService.GetState();
    }

    private sealed class TestChatService : IChatService
    {
        public event EventHandler<ChatEvent>? OnChatMessageReceived;

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Connect(CancellationToken ct) => Task.CompletedTask;

        public Task Disconnect(CancellationToken ct) => Task.CompletedTask;

        public void Publish(PlatformEvent platformEvent)
        {
            OnPlatformEventReceived?.Invoke(this, platformEvent);
            if (platformEvent is ChatEvent chatEvent)
            {
                OnChatMessageReceived?.Invoke(this, chatEvent);
            }
        }
    }

    private sealed class StubQuestionDetectionService : IQuestionDetectionService
    {
        public StubQuestionDetectionService(bool result)
        {
            Result = result;
        }

        public bool Result { get; }

        public Task<bool> IsQuestion(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);
    }
}
