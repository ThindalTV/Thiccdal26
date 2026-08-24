using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// Keeps the lower third to a single slot: a promoted question and operator copy never share the screen.
/// </summary>
public sealed class LowerThirdService : ILowerThirdService, IDisposable
{
    private const string DefaultAccent = "default";

    private readonly IOperatorStateService _operatorStateService;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _stateLock = new();
    private LowerThirdContent? _message;

    public LowerThirdService(IOperatorStateService operatorStateService, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(operatorStateService);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _operatorStateService = operatorStateService;
        _timeProvider = timeProvider;
        _operatorStateService.StateChanged += HandleOperatorStateChanged;
    }

    public event EventHandler? StateChanged;

    public LowerThirdContent? GetCurrent()
    {
        QueuedQuestion? featuredQuestion = GetFeaturedQuestion();

        if (featuredQuestion is not null)
        {
            return new LowerThirdContent(
                LowerThirdContentKind.Question,
                $"{featuredQuestion.Username} via {featuredQuestion.Platform}",
                featuredQuestion.Text,
                featuredQuestion.PlatformColor,
                featuredQuestion.FeaturedAt ?? featuredQuestion.ReceivedAt,
                featuredQuestion.Id);
        }

        lock (_stateLock)
        {
            return _message;
        }
    }

    public void ShowMessage(string eyebrow, string text, string? accent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        lock (_stateLock)
        {
            _message = new LowerThirdContent(
                LowerThirdContentKind.Message,
                eyebrow?.Trim() ?? string.Empty,
                text.Trim(),
                string.IsNullOrWhiteSpace(accent) ? DefaultAccent : accent.Trim(),
                _timeProvider.GetUtcNow(),
                null);
        }

        // Completing the featured question raises StateChanged on its own, which is why the
        // message is stored first: the handler must already see the copy that replaces it.
        ClearFeaturedQuestion();
        RaiseStateChanged();
    }

    public void Clear()
    {
        lock (_stateLock)
        {
            _message = null;
        }

        ClearFeaturedQuestion();
        RaiseStateChanged();
    }

    public void Dispose()
    {
        _operatorStateService.StateChanged -= HandleOperatorStateChanged;
    }

    private void HandleOperatorStateChanged(object? sender, EventArgs args)
    {
        // A question promoted from the queue takes the slot, so stale operator copy is dropped.
        if (GetFeaturedQuestion() is not null)
        {
            lock (_stateLock)
            {
                _message = null;
            }
        }

        RaiseStateChanged();
    }

    private QueuedQuestion? GetFeaturedQuestion()
    {
        return _operatorStateService.QuestionQueue
            .FirstOrDefault(static question => question.State == QuestionState.Featured);
    }

    private void ClearFeaturedQuestion()
    {
        if (GetFeaturedQuestion() is QueuedQuestion featuredQuestion)
        {
            _operatorStateService.CompleteQuestion(featuredQuestion.Id);
        }
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
