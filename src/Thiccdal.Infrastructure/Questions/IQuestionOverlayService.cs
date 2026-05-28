namespace Thiccdal.Infrastructure.Questions;

public interface IQuestionOverlayService
{
    event EventHandler? StateChanged;

    QuestionDashboardState GetState();

    void SetAutoDetect(bool enabled);

    QuestionQueueItem? TryEnqueueDetectedQuestion(
        string platform,
        string username,
        string text,
        string? platformColor = null,
        DateTimeOffset? receivedAt = null);

    QuestionQueueItem AddManualQuestion(string text, string username = "Operator");

    bool TrySelectQuestion(Guid questionId);

    bool TryPromoteSelectedQuestion();

    bool TryPromoteQuestion(Guid questionId);

    bool TryDismissQuestion(Guid questionId);

    bool TryDismissLiveQuestion();

    void ClearWaitingQuestions();
}