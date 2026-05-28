using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class QuestionLocatorServiceTests
{
    [Theory]
    [InlineData("Is this queued?")]
    [InlineData("?is this queued")]
    public async Task WhenDetectorAcceptsMessage_ThenQuestionIsReturned(string content)
    {
        QuestionLocatorService service = new(new StubQuestionDetectionService(true));

        string? question = await service.TryLocateQuestion(CreateChatEvent(content));

        Assert.Equal(content, question);
    }

    [Fact]
    public async Task WhenMessageDoesNotStartWithQuestionMarkOrEndWithQuestionMark_ThenQuestionIsNotReturned()
    {
        QuestionLocatorService service = new(new StubQuestionDetectionService(true));

        string? question = await service.TryLocateQuestion(
            CreateChatEvent(
                "Can you clip that? Kappa",
                [
                    new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Text,
                        Text = "Can you clip that? "
                    },
                    new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Emote,
                        Text = "Kappa",
                        AssetUrl = "https://example.com/kappa.png"
                    }
                ]));

        Assert.Null(question);
    }

    [Fact]
    public async Task WhenDetectorRejectsMessage_ThenNoQuestionIsReturned()
    {
        QuestionLocatorService service = new(new StubQuestionDetectionService(false));

        string? question = await service.TryLocateQuestion(CreateChatEvent("That was great!"));

        Assert.Null(question);
    }

    [Fact]
    public async Task WhenContentIsWhitespace_ThenDetectorIsNotCalled()
    {
        StubQuestionDetectionService detector = new(true);
        QuestionLocatorService service = new(detector);

        string? question = await service.TryLocateQuestion(CreateChatEvent("   "));

        Assert.Null(question);
        Assert.Empty(detector.Messages);
    }

    [Fact]
    public async Task WhenQuestionRuleMatches_ThenNormalizedContentIsTrimmedBeforeEvaluation()
    {
        StubQuestionDetectionService detector = new(true);
        QuestionLocatorService service = new(detector);

        string? question = await service.TryLocateQuestion(CreateChatEvent("  Can you repeat that?  "));

        Assert.Equal("Can you repeat that?", question);
        Assert.Empty(detector.Messages);
    }

    private static ChatEvent CreateChatEvent(string content, IReadOnlyList<ChatMessagePart>? parts = null) =>
        new()
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.ChatMessage,
            Author = "Viewer",
            Channel = "thiccdal",
            Summary = content,
            Content = content,
            Parts = parts ?? [new ChatMessagePart { Type = ChatMessagePartType.Text, Text = content }]
        };

    private sealed class StubQuestionDetectionService : IQuestionDetectionService
    {
        public StubQuestionDetectionService(bool result)
        {
            Result = result;
        }

        public List<string> Messages { get; } = [];

        public bool Result { get; }

        public Task<bool> IsQuestion(string message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(Result);
        }
    }
}
