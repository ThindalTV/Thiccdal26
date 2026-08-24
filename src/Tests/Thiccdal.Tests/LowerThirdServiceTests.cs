using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Tests;

public sealed class LowerThirdServiceTests
{
    [Fact]
    public void WhenNothingIsShowing_ThenCurrentContentIsNull()
    {
        using OperatorStateService operatorStateService = new();
        using LowerThirdService lowerThirdService = CreateService(operatorStateService);

        Assert.Null(lowerThirdService.GetCurrent());
    }

    [Fact]
    public void WhenQuestionIsFeatured_ThenCurrentContentIsTheQuestion()
    {
        using OperatorStateService operatorStateService = new();
        using LowerThirdService lowerThirdService = CreateService(operatorStateService);
        QueuedQuestion question = QueuedQuestion.CreateManual("Why Blazor?");
        operatorStateService.AddQuestion(question);

        operatorStateService.FeatureQuestion(question.Id);

        LowerThirdContent? content = lowerThirdService.GetCurrent();
        Assert.NotNull(content);
        Assert.Equal(LowerThirdContentKind.Question, content.Kind);
        Assert.Equal("Why Blazor?", content.Text);
        Assert.Equal(question.Id, content.QuestionId);
    }

    [Fact]
    public void WhenMessageIsShownWhileQuestionIsLive_ThenQuestionLeavesTheOverlay()
    {
        using OperatorStateService operatorStateService = new();
        using LowerThirdService lowerThirdService = CreateService(operatorStateService);
        QueuedQuestion question = QueuedQuestion.CreateManual("Why Blazor?");
        operatorStateService.AddQuestion(question);
        operatorStateService.FeatureQuestion(question.Id);

        lowerThirdService.ShowMessage("DISCORD", "Join the community!");

        LowerThirdContent? content = lowerThirdService.GetCurrent();
        Assert.NotNull(content);
        Assert.Equal(LowerThirdContentKind.Message, content.Kind);
        Assert.Equal("Join the community!", content.Text);
        Assert.DoesNotContain(operatorStateService.QuestionQueue, item => item.State == QuestionState.Featured);
    }

    [Fact]
    public void WhenQuestionIsFeaturedWhileMessageIsLive_ThenMessageIsDropped()
    {
        using OperatorStateService operatorStateService = new();
        using LowerThirdService lowerThirdService = CreateService(operatorStateService);
        lowerThirdService.ShowMessage("DISCORD", "Join the community!");
        QueuedQuestion question = QueuedQuestion.CreateManual("Why Blazor?");
        operatorStateService.AddQuestion(question);

        operatorStateService.FeatureQuestion(question.Id);
        operatorStateService.CompleteQuestion(question.Id);

        Assert.Null(lowerThirdService.GetCurrent());
    }

    [Fact]
    public void WhenClearIsCalled_ThenBothMessageAndQuestionLeaveTheOverlay()
    {
        using OperatorStateService operatorStateService = new();
        using LowerThirdService lowerThirdService = CreateService(operatorStateService);
        QueuedQuestion question = QueuedQuestion.CreateManual("Why Blazor?");
        operatorStateService.AddQuestion(question);
        operatorStateService.FeatureQuestion(question.Id);

        lowerThirdService.Clear();

        Assert.Null(lowerThirdService.GetCurrent());
        Assert.DoesNotContain(operatorStateService.QuestionQueue, item => item.State == QuestionState.Featured);
    }

    [Fact]
    public void WhenMessageIsShown_ThenStateChangedIsRaised()
    {
        using OperatorStateService operatorStateService = new();
        using LowerThirdService lowerThirdService = CreateService(operatorStateService);
        int stateChangedCount = 0;
        lowerThirdService.StateChanged += (_, _) => stateChangedCount++;

        lowerThirdService.ShowMessage("DISCORD", "Join the community!");

        Assert.True(stateChangedCount > 0);
    }

    private static LowerThirdService CreateService(IOperatorStateService operatorStateService)
    {
        return new LowerThirdService(operatorStateService, TimeProvider.System);
    }
}
