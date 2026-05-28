using Thiccdal.Infrastructure.Questions;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Teleprompter;

namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Coordinates shared operator-facing runtime state across dashboard, overlay, and prompter sessions.
/// </summary>
public interface IOperatorStateService
{
    /// <summary>
    /// Raised whenever shared operator-visible state changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Raised when an overlay test is requested for a named component.
    /// </summary>
    event EventHandler<string>? OverlayTestTriggered;

    /// <summary>
    /// Gets the current operator mode.
    /// </summary>
    OperatorMode Mode { get; }

    /// <summary>
    /// Gets the staged stream title used during pre-live preparation.
    /// </summary>
    string StreamTitle { get; }

    /// <summary>
    /// Gets the staged stream category used during pre-live preparation.
    /// </summary>
    string StreamCategory { get; }

    /// <summary>
    /// Gets the staged stream tags used during pre-live preparation.
    /// </summary>
    IReadOnlyList<string> StreamTags { get; }

    /// <summary>
    /// Gets the current live-start timestamp when the operator is live.
    /// </summary>
    DateTimeOffset? LiveStartedAt { get; }

    /// <summary>
    /// Gets the shared teleprompter scroll position in pixels.
    /// </summary>
    int TeleprompterScrollPosition { get; }

    /// <summary>
    /// Gets the shared question queue snapshot for the current operator session.
    /// </summary>
    IReadOnlyList<QueuedQuestion> QuestionQueue { get; }

    /// <summary>
    /// Gets the current question queue state snapshot.
    /// </summary>
    /// <returns>The current question state.</returns>
    QuestionDashboardState GetQuestionState();

    /// <summary>
    /// Gets the current active stream metadata snapshot when one is known.
    /// </summary>
    /// <returns>The current active stream state, or <c>null</c> when the operator is offline.</returns>
    OperatorStreamState? GetActiveStreamState();

    /// <summary>
    /// Triggers an overlay test across all connected sessions for the specified component.
    /// </summary>
    /// <param name="componentName">The human-readable component name.</param>
    void TriggerOverlayTest(string componentName);

    /// <summary>
    /// Applies a shared teleprompter scroll change.
    /// </summary>
    /// <param name="direction">The scroll direction.</param>
    void ScrollTeleprompter(ScrollDirection direction);

    /// <summary>
    /// Adds a question to the shared operator queue.
    /// </summary>
    /// <param name="question">The question to queue.</param>
    void AddQuestion(QueuedQuestion question);

    /// <summary>
    /// Removes a queued question from active operator use.
    /// </summary>
    /// <param name="questionId">The queued question identifier.</param>
    void DismissQuestion(Guid questionId);

    /// <summary>
    /// Marks a queued question as featured on the lower third.
    /// </summary>
    /// <param name="questionId">The queued question identifier.</param>
    void FeatureQuestion(Guid questionId);

    /// <summary>
    /// Completes a queued question and clears it from the lower third when needed.
    /// </summary>
    /// <param name="questionId">The queued question identifier.</param>
    void CompleteQuestion(Guid questionId);

    /// <summary>
    /// Sets the current operator mode.
    /// </summary>
    /// <param name="mode">The new operator mode.</param>
    void SetMode(OperatorMode mode);

    /// <summary>
    /// Stores the staged stream metadata used during pre-live setup.
    /// </summary>
    /// <param name="title">The staged title.</param>
    /// <param name="category">The staged category.</param>
    /// <param name="tags">The staged tags.</param>
    void SetStreamInfo(string title, string category, IReadOnlyList<string> tags);

    /// <summary>
    /// Transitions the operator session into live mode using the currently staged stream metadata.
    /// </summary>
    /// <param name="startedAt">An optional live-start timestamp.</param>
    /// <param name="sessionId">An optional live session identifier for correlating persisted audit data.</param>
    void BeginLiveSession(DateTimeOffset? startedAt = null, Guid? sessionId = null);

    /// <summary>
    /// Gets whether a manual platform reminder has been reviewed for the current session.
    /// </summary>
    /// <param name="platform">The platform name.</param>
    /// <param name="setting">The reminder setting name.</param>
    /// <returns><c>true</c> when the reminder is checked; otherwise <c>false</c>.</returns>
    bool IsManualReminderReviewed(string platform, string setting);

    /// <summary>
    /// Marks a manual platform reminder as reviewed or not reviewed for the current session.
    /// </summary>
    /// <param name="platform">The platform name.</param>
    /// <param name="setting">The reminder setting name.</param>
    /// <param name="isReviewed">The new reviewed state.</param>
    void SetManualReminderReviewed(string platform, string setting, bool isReviewed);

    /// <summary>
    /// Clears reviewed manual reminder state for the next pre-live session.
    /// </summary>
    /// <returns><c>true</c> when any reminder state was cleared.</returns>
    bool ClearManualReminderReviews();

    /// <summary>
    /// Stores the current active stream metadata snapshot.
    /// </summary>
    /// <param name="streamState">The active stream state, or <c>null</c> when offline.</param>
    void SetActiveStreamState(OperatorStreamState? streamState);

    /// <summary>
    /// Gets whether all visible reminders have been reviewed.
    /// </summary>
    /// <param name="reminders">The reminders currently shown to the operator.</param>
    /// <returns><c>true</c> when all reminders are checked; otherwise <c>false</c>.</returns>
    bool AreAllManualRemindersReviewed(IEnumerable<PlatformManualReminder> reminders);
}
