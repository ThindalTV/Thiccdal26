using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.AI;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.AI;

/// <summary>
/// Classifies queue-worthy questions using the repository-owned AI boundary.
/// </summary>
public sealed class OpenAiQuestionDetectionService : IQuestionDetectionService
{
    private readonly IChatCompletionClient _chatCompletionClient;
    private readonly IOptions<QuestionDetectionOptions> _options;
    private readonly ILogger<OpenAiQuestionDetectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiQuestionDetectionService"/> class.
    /// </summary>
    /// <param name="chatCompletionClient">The AI chat-completion client.</param>
    /// <param name="options">Provides configured question-detection settings.</param>
    /// <param name="logger">Writes classification diagnostics.</param>
    public OpenAiQuestionDetectionService(
        IChatCompletionClient chatCompletionClient,
        IOptions<QuestionDetectionOptions> options,
        ILogger<OpenAiQuestionDetectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(chatCompletionClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _chatCompletionClient = chatCompletionClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsQuestion(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        QuestionDetectionOptions options = _options.Value;
        if (!options.Enabled)
        {
            return false;
        }

        try
        {
            AiChatCompletionResult completion = await _chatCompletionClient.CompleteChat(
                new AiChatCompletionRequest(
                    options.Model,
                    [
                        new AiChatMessage(AiChatMessageRole.System, options.SystemPrompt),
                        new AiChatMessage(AiChatMessageRole.User, RenderUserPrompt(options.UserPromptTemplate, message))
                    ],
                    options.Temperature,
                    options.MaxOutputTokenCount),
                cancellationToken);

            if (TryParseDecision(completion.Content, out bool isQuestion))
            {
                _logger.LogInformation(
                    "AI question detection classified chat message as {Classification}.",
                    isQuestion ? "question" : "not-question");
                return isQuestion;
            }

            _logger.LogWarning(
                "AI question detection returned an unparseable decision: {Decision}",
                completion.Content);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "AI question detection returned malformed JSON.");
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "AI question detection request failed.");
            return false;
        }
    }

    private static string RenderUserPrompt(string template, string message) =>
        template.Replace(QuestionDetectionOptions.MessagePlaceholder, message, StringComparison.Ordinal);

    private static bool TryParseDecision(string rawDecision, out bool isQuestion)
    {
        string normalizedDecision = NormalizeDecision(rawDecision);
        if (normalizedDecision.StartsWith("YES", StringComparison.OrdinalIgnoreCase))
        {
            isQuestion = true;
            return true;
        }

        if (normalizedDecision.StartsWith("NO", StringComparison.OrdinalIgnoreCase))
        {
            isQuestion = false;
            return true;
        }

        using JsonDocument document = JsonDocument.Parse(normalizedDecision);
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("isQuestion", out JsonElement isQuestionElement)
            && (isQuestionElement.ValueKind == JsonValueKind.True || isQuestionElement.ValueKind == JsonValueKind.False))
        {
            isQuestion = isQuestionElement.GetBoolean();
            return true;
        }

        isQuestion = false;
        return false;
    }

    private static string NormalizeDecision(string rawDecision)
    {
        string trimmed = rawDecision.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        int firstLineBreak = trimmed.IndexOf('\n');
        int lastFenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstLineBreak < 0 || lastFenceIndex <= firstLineBreak)
        {
            return trimmed.Trim('`', ' ', '\r', '\n');
        }

        return trimmed[(firstLineBreak + 1)..lastFenceIndex].Trim();
    }
}
