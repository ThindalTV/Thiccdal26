using Thiccdal.Infrastructure.Questions;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Teleprompter;
using Microsoft.Extensions.Options;

namespace Thiccdal.Infrastructure.Operators;

public sealed class OperatorStateService : IOperatorStateService, IDisposable
{
    private readonly object _stateLock = new();
    private readonly object _manualReminderLock = new();
    private readonly Dictionary<string, bool> _manualReminderStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _teleprompterScrollStepPx;
    private readonly List<QueuedQuestion> _questions = [];
    private OperatorMode _mode = OperatorMode.PreLive;
    private string _streamTitle = string.Empty;
    private string _streamCategory = string.Empty;
    private IReadOnlyList<string> _streamTags = [];
    private DateTimeOffset? _liveStartedAt;
    private OperatorStreamState? _streamState;
    private int _teleprompterScrollPosition;

    public OperatorStateService()
        : this(Options.Create(new PrompterOptions()))
    {
    }

    public OperatorStateService(IOptions<PrompterOptions> prompterOptions)
    {
        ArgumentNullException.ThrowIfNull(prompterOptions);

        _teleprompterScrollStepPx = Math.Max(1, prompterOptions.Value.ScrollStepPx);
    }

    // State mutations — every one MUST fire StateChanged:
    // [x] SetMode
    // [x] SetStreamInfo
    // [x] ScrollTeleprompter
    // [x] AddQuestion
    // [x] DismissQuestion
    // [x] FeatureQuestion
    // [x] CompleteQuestion
    // [x] TriggerOverlayTest
    // [x] BeginLiveSession
    // [x] SetActiveStreamState
    // [x] SetManualReminderReviewed
    // [x] ClearManualReminderReviews

    public event EventHandler? StateChanged;

    public event EventHandler<string>? OverlayTestTriggered;

    public OperatorMode Mode
    {
        get
        {
            lock (_stateLock)
            {
                return _mode;
            }
        }
    }

    public string StreamTitle
    {
        get
        {
            lock (_stateLock)
            {
                return _streamTitle;
            }
        }
    }

    public string StreamCategory
    {
        get
        {
            lock (_stateLock)
            {
                return _streamCategory;
            }
        }
    }

    public IReadOnlyList<string> StreamTags
    {
        get
        {
            lock (_stateLock)
            {
                return [.. _streamTags];
            }
        }
    }

    public DateTimeOffset? LiveStartedAt
    {
        get
        {
            lock (_stateLock)
            {
                return _liveStartedAt;
            }
        }
    }

    public int TeleprompterScrollPosition
    {
        get
        {
            lock (_stateLock)
            {
                return _teleprompterScrollPosition;
            }
        }
    }

    public IReadOnlyList<QueuedQuestion> QuestionQueue
    {
        get
        {
            lock (_stateLock)
            {
                return [.. _questions];
            }
        }
    }

    public QuestionDashboardState GetQuestionState()
    {
        QueuedQuestion[] questions;

        lock (_stateLock)
        {
            questions = [.. _questions];
        }

        QuestionQueueItem[] waitingQuestions =
        [
            .. questions
                .Where(static question => question.State == QuestionState.Queued)
                .Select(static question => new QuestionQueueItem(
                    question.Id,
                    question.Platform,
                    question.PlatformColor,
                    question.Username,
                    question.Text,
                    question.ReceivedAt,
                    question.IsManual))
        ];

        QueuedQuestion? featuredQuestion = questions.FirstOrDefault(static question => question.State == QuestionState.Featured);
        LiveQuestion? liveQuestion = featuredQuestion is null
            ? null
            : new LiveQuestion(
                featuredQuestion.Id,
                featuredQuestion.Platform,
                featuredQuestion.PlatformColor,
                featuredQuestion.Username,
                featuredQuestion.Text,
                featuredQuestion.ReceivedAt,
                featuredQuestion.FeaturedAt ?? featuredQuestion.ReceivedAt);

        return new QuestionDashboardState(
            AutoDetectEnabled: true,
            WaitingQuestions: waitingQuestions,
            SelectedQuestion: waitingQuestions.FirstOrDefault(),
            LiveQuestion: liveQuestion,
            AttentionSequence: 0);
    }

    public OperatorStreamState? GetActiveStreamState()
    {
        lock (_stateLock)
        {
            return _streamState;
        }
    }

    public void TriggerOverlayTest(string componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            return;
        }

        EventHandler<string>? overlayHandler = OverlayTestTriggered;
        overlayHandler?.Invoke(this, componentName);
        NotifyChanged();
    }

    public void ScrollTeleprompter(ScrollDirection direction)
    {
        lock (_stateLock)
        {
            _teleprompterScrollPosition = direction switch
            {
                ScrollDirection.Up => Math.Max(0, _teleprompterScrollPosition - _teleprompterScrollStepPx),
                ScrollDirection.Down => _teleprompterScrollPosition + _teleprompterScrollStepPx,
                ScrollDirection.Reset => 0,
                _ => _teleprompterScrollPosition
            };
        }

        NotifyChanged();
    }

    public void AddQuestion(QueuedQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        lock (_stateLock)
        {
            _questions.Insert(0, NormalizeQuestion(question));
        }

        NotifyChanged();
    }

    public void DismissQuestion(Guid questionId)
    {
        bool changed;

        lock (_stateLock)
        {
            changed = TryUpdateQuestionState(questionId, QuestionState.Dismissed);
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public void FeatureQuestion(Guid questionId)
    {
        lock (_stateLock)
        {
            int currentFeaturedIndex = _questions.FindIndex(static question => question.State == QuestionState.Featured);
            if (currentFeaturedIndex >= 0)
            {
                QueuedQuestion currentFeatured = _questions[currentFeaturedIndex];
                _questions[currentFeaturedIndex] = currentFeatured with
                {
                    State = QuestionState.Queued,
                    FeaturedAt = null
                };
            }

            int targetIndex = _questions.FindIndex(question => question.Id == questionId);
            if (targetIndex < 0)
            {
                throw new InvalidOperationException($"Question {questionId} not found.");
            }

            QueuedQuestion target = _questions[targetIndex];
            if (target.State == QuestionState.Dismissed || target.State == QuestionState.Completed)
            {
                throw new InvalidOperationException($"Question {questionId} is no longer active.");
            }

            _questions[targetIndex] = target with
            {
                State = QuestionState.Featured,
                FeaturedAt = DateTimeOffset.UtcNow
            };
        }

        NotifyChanged();
    }

    public void CompleteQuestion(Guid questionId)
    {
        bool changed;

        lock (_stateLock)
        {
            changed = TryUpdateQuestionState(questionId, QuestionState.Completed);
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public void SetMode(OperatorMode mode)
    {
        lock (_stateLock)
        {
            _mode = mode;

            if (mode == OperatorMode.PreLive)
            {
                _liveStartedAt = null;
                _streamState = null;
                _teleprompterScrollPosition = 0;
                _questions.Clear();
            }
        }

        NotifyChanged();
    }

    public void SetStreamInfo(string title, string category, IReadOnlyList<string> tags)
    {
        IReadOnlyList<string> normalizedTags = NormalizeTags(tags);
        string normalizedTitle = title?.Trim() ?? string.Empty;
        string normalizedCategory = category?.Trim() ?? string.Empty;

        lock (_stateLock)
        {
            _streamTitle = normalizedTitle;
            _streamCategory = normalizedCategory;
            _streamTags = normalizedTags;
        }

        NotifyChanged();
    }

    public void BeginLiveSession(DateTimeOffset? startedAt = null, Guid? sessionId = null)
    {
        DateTimeOffset effectiveStartedAt = startedAt ?? DateTimeOffset.UtcNow;

        lock (_stateLock)
        {
            _streamState = new OperatorStreamState
            {
                SessionId = sessionId,
                Title = _streamTitle,
                Category = _streamCategory,
                Tags = [.. _streamTags],
                StartedAt = effectiveStartedAt
            };
            _liveStartedAt = effectiveStartedAt;
            _mode = OperatorMode.Live;
        }

        NotifyChanged();
    }

    public bool IsManualReminderReviewed(string platform, string setting)
    {
        string key = BuildManualReminderKey(platform, setting);

        lock (_manualReminderLock)
        {
            return _manualReminderStates.GetValueOrDefault(key, false);
        }
    }

    public void SetManualReminderReviewed(string platform, string setting, bool isReviewed)
    {
        string key = BuildManualReminderKey(platform, setting);
        bool changed;

        lock (_manualReminderLock)
        {
            changed = _manualReminderStates.GetValueOrDefault(key, false) != isReviewed;

            if (isReviewed)
            {
                _manualReminderStates[key] = true;
            }
            else
            {
                _manualReminderStates.Remove(key);
            }
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public bool ClearManualReminderReviews()
    {
        bool changed;

        lock (_manualReminderLock)
        {
            changed = _manualReminderStates.Count > 0;
            _manualReminderStates.Clear();
        }

        if (changed)
        {
            NotifyChanged();
        }

        return changed;
    }

    public void SetActiveStreamState(OperatorStreamState? streamState)
    {
        OperatorStreamState? normalizedState = NormalizeStreamState(streamState);

        lock (_stateLock)
        {
            _streamState = normalizedState;
            _liveStartedAt = normalizedState?.StartedAt;
            _mode = normalizedState is null ? OperatorMode.PreLive : OperatorMode.Live;
        }

        NotifyChanged();
    }

    public bool AreAllManualRemindersReviewed(IEnumerable<PlatformManualReminder> reminders)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        PlatformManualReminder[] reminderArray = reminders.ToArray();
        if (reminderArray.Length == 0)
        {
            return false;
        }

        lock (_manualReminderLock)
        {
            return reminderArray.All(reminder =>
                _manualReminderStates.GetValueOrDefault(
                    BuildManualReminderKey(reminder.Platform, reminder.Setting),
                    false));
        }
    }

    private static string BuildManualReminderKey(string platform, string setting)
    {
        return $"{platform.Trim()}::{setting.Trim()}";
    }

    private static OperatorStreamState? NormalizeStreamState(OperatorStreamState? streamState)
    {
        if (streamState is null)
        {
            return null;
        }

        return new OperatorStreamState
        {
            SessionId = streamState.SessionId,
            Title = streamState.Title,
            Category = streamState.Category,
            Tags = NormalizeTags(streamState.Tags),
            StartedAt = streamState.StartedAt
        };
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return tags
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Select(static tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static QueuedQuestion NormalizeQuestion(QueuedQuestion question)
    {
        return question with
        {
            Platform = string.IsNullOrWhiteSpace(question.Platform) ? "UNKNOWN" : question.Platform.Trim().ToUpperInvariant(),
            PlatformColor = string.IsNullOrWhiteSpace(question.PlatformColor) ? "default" : question.PlatformColor.Trim(),
            Username = string.IsNullOrWhiteSpace(question.Username) ? "Viewer" : question.Username.Trim(),
            Text = question.Text.Trim()
        };
    }

    private bool TryUpdateQuestionState(Guid questionId, QuestionState state)
    {
        int questionIndex = _questions.FindIndex(question => question.Id == questionId);
        if (questionIndex < 0)
        {
            return false;
        }

        QueuedQuestion question = _questions[questionIndex];
        _questions[questionIndex] = question with
        {
            State = state,
            FeaturedAt = state == QuestionState.Featured ? question.FeaturedAt : null
        };

        return true;
    }

    private void NotifyChanged()
    {
        EventHandler? handler = StateChanged;
        handler?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
