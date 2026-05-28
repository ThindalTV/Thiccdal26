using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Modules.ChatBot.Services;

/// <summary>
/// Locates queue-worthy questions from normalized chat events.
/// </summary>
public sealed class QuestionLocatorService : IQuestionLocatorService
{
    public QuestionLocatorService()
    {
    }

    public QuestionLocatorService(IQuestionDetectionService questionDetectionService)
        : this()
    {
        ArgumentNullException.ThrowIfNull(questionDetectionService);
    }

    /// <inheritdoc />
    public async Task<string?> TryLocateQuestion(ChatEvent chatEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatEvent);
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedContent = chatEvent.Content.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return null;
        }

        return LooksLikeQuestion(normalizedContent) ? normalizedContent : null;
    }

    private static bool LooksLikeQuestion(string normalizedContent)
    {
        return normalizedContent.StartsWith("?", StringComparison.Ordinal) ||
               normalizedContent.EndsWith("?", StringComparison.Ordinal);
    }
}
