using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Tests;

public sealed class QuestionOverlayServiceTests
{
    [Fact]
    public void WhenConstructed_ThenFirstQueuedQuestionIsSelected()
    {
        QuestionOverlayService service = new();

        QuestionDashboardState state = service.GetState();

        Assert.NotEmpty(state.WaitingQuestions);
        Assert.Equal(state.WaitingQuestions[0].Id, state.SelectedQuestion?.Id);
    }

    [Fact]
    public void WhenPromotingSelectedQuestion_ThenQuestionMovesToLiveLowerThird()
    {
        QuestionOverlayService service = new();
        QuestionQueueItem selectedQuestion = service.GetState().SelectedQuestion!;

        bool promoted = service.TryPromoteSelectedQuestion();

        QuestionDashboardState state = service.GetState();
        Assert.True(promoted);
        Assert.NotNull(state.LiveQuestion);
        Assert.Equal(selectedQuestion.Text, state.LiveQuestion?.Text);
        Assert.DoesNotContain(state.WaitingQuestions, question => question.Id == selectedQuestion.Id);
    }

    [Fact]
    public void WhenAutoDetectIsDisabled_ThenDetectedQuestionsAreIgnored()
    {
        QuestionOverlayService service = new();
        int initialCount = service.GetState().WaitingQuestions.Count;

        service.SetAutoDetect(false);
        QuestionQueueItem? question = service.TryEnqueueDetectedQuestion("Twitch", "Viewer", "Will this be queued?");

        QuestionDashboardState state = service.GetState();
        Assert.Null(question);
        Assert.Equal(initialCount, state.WaitingQuestions.Count);
    }

    [Fact]
    public void WhenAddingManualQuestion_ThenItBecomesSelectedEvenIfAutoDetectIsOff()
    {
        QuestionOverlayService service = new();
        service.SetAutoDetect(false);

        QuestionQueueItem question = service.AddManualQuestion("Manual operator prompt");

        QuestionDashboardState state = service.GetState();
        Assert.Equal(question.Id, state.SelectedQuestion?.Id);
        Assert.Equal("MANUAL", state.SelectedQuestion?.Platform);
        Assert.Contains(state.WaitingQuestions, candidate => candidate.Id == question.Id);
    }

    [Fact]
    public void WhenNewQuestionArrives_ThenAttentionSequenceAdvances()
    {
        QuestionOverlayService service = new();
        long initialAttentionSequence = service.GetState().AttentionSequence;

        service.TryEnqueueDetectedQuestion("Twitch", "Viewer", "Did a new one land?");
        QuestionQueueItem manualQuestion = service.AddManualQuestion("Operator question");

        QuestionDashboardState state = service.GetState();

        Assert.Equal(initialAttentionSequence + 2, state.AttentionSequence);
        Assert.Equal(manualQuestion.Id, state.SelectedQuestion?.Id);
    }

    [Fact]
    public void WhenManagingExistingQuestions_ThenAttentionSequenceDoesNotAdvance()
    {
        QuestionOverlayService service = new();
        QuestionQueueItem manualQuestion = service.AddManualQuestion("Hold this question");
        long attentionSequence = service.GetState().AttentionSequence;

        service.TrySelectQuestion(service.GetState().WaitingQuestions[^1].Id);
        service.TryPromoteQuestion(manualQuestion.Id);
        service.TryDismissLiveQuestion();

        QuestionDashboardState state = service.GetState();

        Assert.Equal(attentionSequence, state.AttentionSequence);
    }
}