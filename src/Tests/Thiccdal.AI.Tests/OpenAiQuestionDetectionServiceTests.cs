using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.AI;
using Thiccdal.Infrastructure.AI;

namespace Thiccdal.AI.Tests;

public sealed class OpenAiQuestionDetectionServiceTests
{
    [Fact]
    public async Task WhenQuestionDetectionIsDisabled_ThenQuestionIsRejected()
    {
        StubChatCompletionClient client = new(new AiChatCompletionResult("YES", "model", "stop"));
        OpenAiQuestionDetectionService service = new(
            client,
            Options.Create(new QuestionDetectionOptions()),
            NullLogger<OpenAiQuestionDetectionService>.Instance);

        bool isQuestion = await service.IsQuestion("Could you explain that?");

        Assert.False(isQuestion);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task WhenModelReturnsJsonDecision_ThenQuestionIsAccepted()
    {
        StubChatCompletionClient client = new(new AiChatCompletionResult("```json\n{\"isQuestion\":true}\n```", "model", "stop"));
        OpenAiQuestionDetectionService service = new(
            client,
            Options.Create(
                new QuestionDetectionOptions
                {
                    Enabled = true,
                    Model = "question-model"
                }),
            NullLogger<OpenAiQuestionDetectionService>.Instance);

        bool isQuestion = await service.IsQuestion("Could you explain that?");

        Assert.True(isQuestion);
        Assert.Equal(1, client.CallCount);
        Assert.Equal("question-model", client.LastRequest?.Model);
    }

    [Fact]
    public async Task WhenModelThrows_ThenQuestionIsRejected()
    {
        ThrowingChatCompletionClient client = new();
        OpenAiQuestionDetectionService service = new(
            client,
            Options.Create(
                new QuestionDetectionOptions
                {
                    Enabled = true,
                    Model = "question-model"
                }),
            NullLogger<OpenAiQuestionDetectionService>.Instance);

        bool isQuestion = await service.IsQuestion("What happened?");

        Assert.False(isQuestion);
    }

    private sealed class StubChatCompletionClient : IChatCompletionClient
    {
        private readonly AiChatCompletionResult _result;

        public StubChatCompletionClient(AiChatCompletionResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public AiChatCompletionRequest? LastRequest { get; private set; }

        public Task<AiChatCompletionResult> CompleteChat(
            AiChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingChatCompletionClient : IChatCompletionClient
    {
        public Task<AiChatCompletionResult> CompleteChat(
            AiChatCompletionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Endpoint unavailable");
    }
}
