using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.LmStudio;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Remote.LMStudio;

/// <summary>
/// Classifies queue-worthy questions on top of the reusable LM Studio client.
/// </summary>
public sealed class LmStudioQuestionDetectionService : IQuestionDetectionService
{
    private readonly ILmStudioClient _lmStudioClient;
    private readonly IOptions<LmStudioQuestionDetectionOptions> _options;
    private readonly ILogger<LmStudioQuestionDetectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LmStudioQuestionDetectionService"/> class.
    /// </summary>
    /// <param name="lmStudioClient">The reusable LM Studio client.</param>
    /// <param name="options">Provides configured LM Studio settings.</param>
    /// <param name="logger">Writes classification diagnostics.</param>
    public LmStudioQuestionDetectionService(
        ILmStudioClient lmStudioClient,
        IOptions<LmStudioQuestionDetectionOptions> options,
        ILogger<LmStudioQuestionDetectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(lmStudioClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _lmStudioClient = lmStudioClient;
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

        LmStudioQuestionDetectionOptions options = _options.Value;
        if (!options.Enabled)
        {
            return false;
        }

        try
        {
            LmStudioChatCompletionResult completion = await _lmStudioClient.CompleteChat(
                new LmStudioChatCompletionRequest(
                    options.Model,
                    [
                        new LmStudioChatMessage("system", options.SystemPrompt),
                        new LmStudioChatMessage("user", RenderUserPrompt(options.UserPromptTemplate, message))
                    ],
                    options.Temperature,
                    options.MaxTokens),
                cancellationToken);

            string rawDecision = completion.Content;
            if (TryParseDecision(rawDecision, out bool isQuestion))
            {
                _logger.LogInformation(
                    "LM Studio classified chat message as {Classification}.",
                    isQuestion ? "question" : "not-question");
                return isQuestion;
            }

            _logger.LogWarning(
                "LM Studio question detection returned an unparseable decision: {Decision}",
                completion.Content);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "LM Studio question detection returned malformed JSON.");
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "LM Studio question detection request failed.");
            return false;
        }
    }

    private static string RenderUserPrompt(string template, string message) =>
        template.Replace(LmStudioQuestionDetectionOptions.MessagePlaceholder, message, StringComparison.Ordinal);

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

        try
        {
            using JsonDocument document = JsonDocument.Parse(normalizedDecision);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("isQuestion", out JsonElement isQuestionElement)
                && (isQuestionElement.ValueKind == JsonValueKind.True || isQuestionElement.ValueKind == JsonValueKind.False))
            {
                isQuestion = isQuestionElement.GetBoolean();
                return true;
            }
        }
        catch (JsonException)
        {
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
