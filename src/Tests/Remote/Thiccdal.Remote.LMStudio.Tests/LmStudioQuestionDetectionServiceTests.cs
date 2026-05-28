using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.LmStudio;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Remote.LMStudio.Tests;

public sealed class LmStudioQuestionDetectionServiceTests
{
    [Fact]
    public async Task WhenQuestionDetectionIsDisabled_ThenQuestionIsRejected()
    {
        StubLmStudioClient client = new(new LmStudioChatCompletionResult("YES", "model", "stop"));
        LmStudioQuestionDetectionService service = new(
            client,
            Options.Create(new LmStudioQuestionDetectionOptions()),
            NullLogger<LmStudioQuestionDetectionService>.Instance);

        bool isQuestion = await service.IsQuestion("Could you explain that?");

        Assert.False(isQuestion);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task WhenModelReturnsJsonDecision_ThenQuestionIsAccepted()
    {
        StubLmStudioClient client = new(new LmStudioChatCompletionResult("```json\n{\"isQuestion\":true}\n```", "model", "stop"));
        LmStudioQuestionDetectionService service = new(
            client,
            Options.Create(new LmStudioQuestionDetectionOptions
            {
                Enabled = true,
                Model = "question-model"
            }),
            NullLogger<LmStudioQuestionDetectionService>.Instance);

        bool isQuestion = await service.IsQuestion("Could you explain that?");

        Assert.True(isQuestion);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task WhenLmStudioThrows_ThenQuestionIsRejected()
    {
        ThrowingLmStudioClient client = new();
        LmStudioQuestionDetectionService service = new(
            client,
            Options.Create(new LmStudioQuestionDetectionOptions
            {
                Enabled = true,
                Model = "question-model"
            }),
            NullLogger<LmStudioQuestionDetectionService>.Instance);

        bool isQuestion = await service.IsQuestion("What happened?");

        Assert.False(isQuestion);
    }

    private sealed class StubLmStudioClient : ILmStudioClient
    {
        private readonly LmStudioChatCompletionResult _result;

        public StubLmStudioClient(LmStudioChatCompletionResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<LmStudioChatCompletionResult> CompleteChat(
            LmStudioChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingLmStudioClient : ILmStudioClient
    {
        public Task<LmStudioChatCompletionResult> CompleteChat(
            LmStudioChatCompletionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("LM Studio unavailable");
    }
}
