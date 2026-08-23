using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Tests;

public sealed class OperatorStateServiceTests
{
    [Fact]
    public void WhenOperatorStateServiceCreated_ThenModeStartsPreLive()
    {
        using OperatorStateService operatorStateService = new();

        Assert.Equal(OperatorMode.PreLive, operatorStateService.Mode);
    }

    [Fact]
    public void WhenTriggeringOverlayTest_ThenOverlayTestTriggeredEventRaised()
    {
        using OperatorStateService operatorStateService = new();
        string? raisedComponentName = null;

        operatorStateService.OverlayTestTriggered += (sender, componentName) => raisedComponentName = componentName;

        operatorStateService.TriggerOverlayTest("Chat Feed");

        Assert.Equal("Chat Feed", raisedComponentName);
    }

    [Fact]
    public void WhenManualReminderStateChanges_ThenAllReviewedStateUpdates()
    {
        using OperatorStateService operatorStateService = new();
        PlatformManualReminder[] reminders =
        [
            new PlatformManualReminder { Platform = "Twitch", Setting = "Stream delay", ReminderText = "Check it" },
            new PlatformManualReminder { Platform = "Null", Setting = "Visibility", ReminderText = "Check it too" }
        ];

        operatorStateService.SetManualReminderReviewed("Twitch", "Stream delay", true);
        operatorStateService.SetManualReminderReviewed("Null", "Visibility", true);

        Assert.True(operatorStateService.IsManualReminderReviewed("Twitch", "Stream delay"));
        Assert.True(operatorStateService.AreAllManualRemindersReviewed(reminders));
    }

    [Fact]
    public void WhenQuestionQueueChanges_ThenStateChangedEventIsRaised()
    {
        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        int stateChangedCount = 0;

        operatorStateService.StateChanged += (sender, args) => stateChangedCount++;

        questionOverlayService.AddManualQuestion("What changed?");

        Assert.True(stateChangedCount > 0);
        Assert.Contains(
            operatorStateService.GetQuestionState().WaitingQuestions,
            question => question.Text == "What changed?");
    }

    [Fact]
    public void WhenNewQuestionArrives_ThenQuestionAttentionSequenceIsExposed()
    {
        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        long initialAttentionSequence = questionOverlayService.GetState().AttentionSequence;

        questionOverlayService.TryEnqueueDetectedQuestion("Twitch", "Viewer", "Any follow-up?");

        Assert.Equal(initialAttentionSequence + 1, questionOverlayService.GetState().AttentionSequence);
    }

    [Fact]
    public void WhenActiveStreamStateChanges_ThenSnapshotIsStoredAndStateChangedEventRaised()
    {
        using OperatorStateService operatorStateService = new();
        int stateChangedCount = 0;

        operatorStateService.StateChanged += (_, _) => stateChangedCount++;

        operatorStateService.SetActiveStreamState(new OperatorStreamState
        {
            Title = "Building Thiccdal Live!",
            Category = "Science & Technology",
            Tags = ["dotnet", "blazor"],
            StartedAt = new DateTimeOffset(2024, 6, 1, 14, 0, 0, TimeSpan.Zero)
        });

        OperatorStreamState? streamState = operatorStateService.GetActiveStreamState();

        Assert.NotNull(streamState);
        Assert.Equal("Building Thiccdal Live!", streamState.Title);
        Assert.Equal("Science & Technology", streamState.Category);
        Assert.Equal(["dotnet", "blazor"], streamState.Tags);
        Assert.Equal(1, stateChangedCount);
        Assert.Equal(OperatorMode.Live, operatorStateService.Mode);
    }

    [Fact]
    public void WhenSettingStreamInfo_ThenValuesPersistAcrossReads()
    {
        using OperatorStateService operatorStateService = new();

        operatorStateService.SetStreamInfo("  Pre-live title  ", "  Gaming  ", ["dotnet", "dotnet", "  blazor  ", ""]);

        Assert.Equal("Pre-live title", operatorStateService.StreamTitle);
        Assert.Equal("Gaming", operatorStateService.StreamCategory);
        Assert.Equal(["dotnet", "blazor"], operatorStateService.StreamTags);
    }

    [Fact]
    public void WhenBeginningLiveSession_ThenModeAndActiveStreamUseStagedInfo()
    {
        using OperatorStateService operatorStateService = new();
        DateTimeOffset startedAt = new(2026, 5, 31, 12, 30, 0, TimeSpan.Zero);
        Guid sessionId = Guid.Parse("f4dba53d-6b06-4e3f-9bb6-f67f6e3d0d33");

        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["backend", "services"]);

        operatorStateService.BeginLiveSession(startedAt, sessionId);

        OperatorStreamState? streamState = operatorStateService.GetActiveStreamState();

        Assert.Equal(OperatorMode.Live, operatorStateService.Mode);
        Assert.NotNull(streamState);
        Assert.Equal(sessionId, streamState.SessionId);
        Assert.Equal("Ship it", streamState.Title);
        Assert.Equal("Science & Technology", streamState.Category);
        Assert.Equal(["backend", "services"], streamState.Tags);
        Assert.Equal(startedAt, streamState.StartedAt);
    }

    [Fact]
    public void WhenClearingActiveStreamState_ThenModeReturnsToPreLive()
    {
        using OperatorStateService operatorStateService = new();

        operatorStateService.BeginLiveSession(new DateTimeOffset(2026, 5, 31, 12, 30, 0, TimeSpan.Zero));
        operatorStateService.SetActiveStreamState(null);

        Assert.Equal(OperatorMode.PreLive, operatorStateService.Mode);
        Assert.Null(operatorStateService.GetActiveStreamState());
        Assert.Null(operatorStateService.LiveStartedAt);
    }

    [Fact]
    public void WhenClearingManualReminderReviews_ThenReviewedStateResets()
    {
        using OperatorStateService operatorStateService = new();

        operatorStateService.SetManualReminderReviewed("Twitch", "Visibility", true);

        bool changed = operatorStateService.ClearManualReminderReviews();

        Assert.True(changed);
        Assert.False(operatorStateService.IsManualReminderReviewed("Twitch", "Visibility"));
    }
}
