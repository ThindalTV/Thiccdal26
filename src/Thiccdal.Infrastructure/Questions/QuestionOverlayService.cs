using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Infrastructure.Questions;

public sealed class QuestionOverlayService : IQuestionOverlayService, IDisposable
{
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly IOperatorStateService _operatorStateService;
    private readonly bool _ownsOperatorStateService;
    private bool _autoDetectEnabled = true;
    private long _attentionSequence;
    private Guid? _selectedQuestionId;

    public QuestionOverlayService()
        : this(new OperatorStateService(), TimeProvider.System, true, true)
    {
    }

    public QuestionOverlayService(IOperatorStateService operatorStateService)
        : this(operatorStateService, TimeProvider.System, false, false)
    {
    }

    internal QuestionOverlayService(
        IOperatorStateService operatorStateService,
        TimeProvider clock,
        bool seedQuestions,
        bool ownsOperatorStateService)
    {
        ArgumentNullException.ThrowIfNull(operatorStateService);

        _operatorStateService = operatorStateService;
        _clock = clock;
        _ownsOperatorStateService = ownsOperatorStateService;
        _operatorStateService.StateChanged += HandleOperatorStateChanged;

        if (seedQuestions)
        {
            SeedQuestions(clock.GetUtcNow());
        }

        _selectedQuestionId = GetQueuedQuestions().FirstOrDefault()?.Id;
    }

    public event EventHandler? StateChanged;

    public QuestionDashboardState GetState()
    {
        lock (_gate)
        {
            return CreateStateSnapshotUnsafe(GetQueuedQuestions());
        }
    }

    public void SetAutoDetect(bool enabled)
    {
        bool changed;

        lock (_gate)
        {
            changed = _autoDetectEnabled != enabled;
            _autoDetectEnabled = enabled;
        }

        if (changed)
        {
            NotifyStateChanged();
        }
    }

    public QuestionQueueItem? TryEnqueueDetectedQuestion(
        string platform,
        string username,
        string text,
        string? platformColor = null,
        DateTimeOffset? receivedAt = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        QueuedQuestion? question;

        lock (_gate)
        {
            if (!_autoDetectEnabled)
            {
                return null;
            }

            question = QueuedQuestion.CreateDetected(
                platform,
                username,
                text,
                platformColor,
                receivedAt ?? _clock.GetUtcNow());
            _attentionSequence++;
            _selectedQuestionId ??= question.Id;
        }

        _operatorStateService.AddQuestion(question);
        return ToQueueItem(question);
    }

    public QuestionQueueItem AddManualQuestion(string text, string username = "Operator")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Question text is required.", nameof(text));
        }

        QueuedQuestion question;

        lock (_gate)
        {
            question = QueuedQuestion.CreateManual(text, username, _clock.GetUtcNow());
            _attentionSequence++;
            _selectedQuestionId = question.Id;
        }

        _operatorStateService.AddQuestion(question);
        return ToQueueItem(question);
    }

    public bool TrySelectQuestion(Guid questionId)
    {
        bool changed;

        lock (_gate)
        {
            if (_selectedQuestionId == questionId)
            {
                return false;
            }

            if (!GetQueuedQuestions().Any(question => question.Id == questionId))
            {
                return false;
            }

            _selectedQuestionId = questionId;
            changed = true;
        }

        if (changed)
        {
            NotifyStateChanged();
        }

        return changed;
    }

    public bool TryPromoteSelectedQuestion()
    {
        Guid? selectedQuestionId;

        lock (_gate)
        {
            selectedQuestionId = _selectedQuestionId;
        }

        return selectedQuestionId is Guid questionId && TryPromoteQuestion(questionId);
    }

    public bool TryPromoteQuestion(Guid questionId)
    {
        bool changed = false;

        lock (_gate)
        {
            QueuedQuestion? question = GetQueuedQuestions().FirstOrDefault(candidate => candidate.Id == questionId);
            if (question is null)
            {
                return false;
            }

            changed = true;
        }

        _operatorStateService.FeatureQuestion(questionId);

        lock (_gate)
        {
            _selectedQuestionId = GetQueuedQuestions().FirstOrDefault()?.Id;
            changed = true;
        }

        if (changed)
        {
            NotifyStateChanged();
        }

        return changed;
    }

    public bool TryDismissQuestion(Guid questionId)
    {
        bool changed;

        lock (_gate)
        {
            QueuedQuestion? question = GetQueuedQuestions().FirstOrDefault(candidate => candidate.Id == questionId);
            if (question is null)
            {
                return false;
            }

            if (_selectedQuestionId == questionId)
            {
                _selectedQuestionId = GetQueuedQuestions().Where(question => question.Id != questionId).FirstOrDefault()?.Id;
            }

            changed = true;
        }

        _operatorStateService.DismissQuestion(questionId);

        if (changed)
        {
            NotifyStateChanged();
        }

        return changed;
    }

    public bool TryDismissLiveQuestion()
    {
        bool changed;
        Guid? featuredQuestionId;

        lock (_gate)
        {
            featuredQuestionId = _operatorStateService.QuestionQueue
                .FirstOrDefault(static question => question.State == QuestionState.Featured)
                ?.Id;
            changed = featuredQuestionId is not null;
        }

        if (featuredQuestionId is not null)
        {
            _operatorStateService.CompleteQuestion(featuredQuestionId.Value);
        }

        return changed;
    }

    public void ClearWaitingQuestions()
    {
        bool changed;
        Guid[] queuedQuestionIds;

        lock (_gate)
        {
            queuedQuestionIds =
            [
                .. GetQueuedQuestions().Select(static question => question.Id)
            ];
            changed = queuedQuestionIds.Length > 0 || _selectedQuestionId is not null;
            _selectedQuestionId = null;
        }

        if (changed)
        {
            foreach (Guid queuedQuestionId in queuedQuestionIds)
            {
                _operatorStateService.DismissQuestion(queuedQuestionId);
            }
        }
    }

    public void Dispose()
    {
        _operatorStateService.StateChanged -= HandleOperatorStateChanged;

        if (_ownsOperatorStateService && _operatorStateService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void HandleOperatorStateChanged(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            if (_selectedQuestionId is Guid selectedQuestionId
                && !GetQueuedQuestions().Any(question => question.Id == selectedQuestionId))
            {
                _selectedQuestionId = GetQueuedQuestions().FirstOrDefault()?.Id;
            }
        }

        NotifyStateChanged();
    }

    private QuestionDashboardState CreateStateSnapshotUnsafe(IReadOnlyList<QueuedQuestion> queuedQuestions)
    {
        QuestionQueueItem[] waitingQuestions =
        [
            .. queuedQuestions.Select(ToQueueItem)
        ];
        QuestionQueueItem? selectedQuestion = waitingQuestions.FirstOrDefault(question => question.Id == _selectedQuestionId)
            ?? waitingQuestions.FirstOrDefault();
        QueuedQuestion? liveQuestionSource = _operatorStateService.QuestionQueue
            .FirstOrDefault(static question => question.State == QuestionState.Featured);
        LiveQuestion? liveQuestion = liveQuestionSource is null
            ? null
            : new LiveQuestion(
                liveQuestionSource.Id,
                liveQuestionSource.Platform,
                liveQuestionSource.PlatformColor,
                liveQuestionSource.Username,
                liveQuestionSource.Text,
                liveQuestionSource.ReceivedAt,
                liveQuestionSource.FeaturedAt ?? liveQuestionSource.ReceivedAt);

        return new QuestionDashboardState(
            AutoDetectEnabled: _autoDetectEnabled,
            WaitingQuestions: waitingQuestions,
            SelectedQuestion: selectedQuestion,
            LiveQuestion: liveQuestion,
            AttentionSequence: _attentionSequence);
    }

    private IReadOnlyList<QueuedQuestion> GetQueuedQuestions()
    {
        return _operatorStateService.QuestionQueue
            .Where(static question => question.State == QuestionState.Queued)
            .ToArray();
    }

    private void SeedQuestions(DateTimeOffset now)
    {
        QueuedQuestion[] seedQuestions =
        [
            QueuedQuestion.CreateDetected("YOUTUBE", "TechWatcher", "How long have you been streaming and what got you into it?", receivedAt: now.AddMinutes(-5)),
            QueuedQuestion.CreateDetected("TWITCH", "PurplePanda", "Are you planning to do more collab streams?", receivedAt: now.AddMinutes(-8)),
            QueuedQuestion.CreateDetected("KICK", "KickViewer42", "What game are you going to play next session?", receivedAt: now.AddMinutes(-15)),
            QueuedQuestion.CreateDetected("YOUTUBE", "GamingNerd", "Do you stream every day or on a schedule?", receivedAt: now.AddMinutes(-18)),
            QueuedQuestion.CreateDetected("TWITCH", "StreamFan99", "What's the best advice you'd give to someone just starting out on Twitch?", receivedAt: now.AddMinutes(-22))
        ];

        foreach (QueuedQuestion question in seedQuestions.Reverse())
        {
            _operatorStateService.AddQuestion(question);
        }
    }

    private static QuestionQueueItem ToQueueItem(QueuedQuestion question)
    {
        return new QuestionQueueItem(
            question.Id,
            question.Platform,
            question.PlatformColor,
            question.Username,
            question.Text,
            question.ReceivedAt,
            question.IsManual);
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}