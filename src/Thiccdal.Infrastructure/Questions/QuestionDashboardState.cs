namespace Thiccdal.Infrastructure.Questions;

public sealed record QuestionDashboardState(
    bool AutoDetectEnabled,
    IReadOnlyList<QuestionQueueItem> WaitingQuestions,
    QuestionQueueItem? SelectedQuestion,
    LiveQuestion? LiveQuestion,
    long AttentionSequence)
{
    public static QuestionDashboardState Empty { get; } = new(
        AutoDetectEnabled: true,
        WaitingQuestions: Array.Empty<QuestionQueueItem>(),
        SelectedQuestion: null,
        LiveQuestion: null,
        AttentionSequence: 0);
}