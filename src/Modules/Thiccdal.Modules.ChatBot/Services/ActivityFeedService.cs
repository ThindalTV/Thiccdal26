using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Modules.ChatBot.Services;

/// <summary>
/// Maintains a shared in-memory activity feed for the prompter and overlay.
/// </summary>
public class ActivityFeedService : IActivityFeedService, IHostedService, IDisposable
{
    private const int MaxEntries = 200;

    private readonly IChatService _chatService;
    private readonly IQuestionLocatorService _questionLocatorService;
    private readonly IQuestionOverlayService _questionOverlayService;
    private readonly IOptions<ChatBotOptions> _chatBotOptions;
    private readonly ILogger<ActivityFeedService> _logger;
    private readonly object _syncRoot = new();
    private readonly object _lifecycleSyncRoot = new();
    private readonly List<ActivityFeedEntry> _entries = [];
    private readonly Channel<ChatEvent> _questionCandidates = Channel.CreateUnbounded<ChatEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private CancellationTokenSource? _questionProcessingCancellationTokenSource;
    private Task? _questionProcessingTask;
    private bool _started;

    public event EventHandler<ActivityFeedEntry>? EntryAdded;

    public ActivityFeedService(
        IChatService chatService,
        IQuestionLocatorService questionLocatorService,
        IQuestionOverlayService questionOverlayService,
        IOptions<ChatBotOptions> chatBotOptions,
        ILogger<ActivityFeedService> logger)
    {
        _chatService = chatService;
        _questionLocatorService = questionLocatorService;
        _questionOverlayService = questionOverlayService;
        _chatBotOptions = chatBotOptions;
        _logger = logger;
    }

    public IReadOnlyList<ActivityFeedEntry> GetEntries()
    {
        lock (_syncRoot)
        {
            return _entries.ToArray();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lifecycleSyncRoot)
        {
            if (_started)
            {
                return Task.CompletedTask;
            }

            _questionProcessingCancellationTokenSource = new CancellationTokenSource();
            _questionProcessingTask = ProcessQuestionCandidates(_questionProcessingCancellationTokenSource.Token);
            _chatService.OnPlatformEventReceived += HandlePlatformEventReceived;
            _started = true;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? questionProcessingTask;
        CancellationTokenSource? questionProcessingCancellationTokenSource;

        lock (_lifecycleSyncRoot)
        {
            if (!_started)
            {
                return;
            }

            _chatService.OnPlatformEventReceived -= HandlePlatformEventReceived;
            _questionCandidates.Writer.TryComplete();
            _started = false;
            questionProcessingTask = _questionProcessingTask;
            questionProcessingCancellationTokenSource = _questionProcessingCancellationTokenSource;
            _questionProcessingCancellationTokenSource = null;
            _questionProcessingTask = null;
        }

        if (questionProcessingTask is null)
        {
            questionProcessingCancellationTokenSource?.Dispose();
            return;
        }

        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((CancellationTokenSource)state!).Cancel(),
            questionProcessingCancellationTokenSource);

        try
        {
            await questionProcessingTask;
        }
        finally
        {
            questionProcessingCancellationTokenSource?.Dispose();
        }
    }

    public void Dispose()
    {
        _chatService.OnPlatformEventReceived -= HandlePlatformEventReceived;
        _questionCandidates.Writer.TryComplete();
        _questionProcessingCancellationTokenSource?.Cancel();
        _questionProcessingCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void HandlePlatformEventReceived(object? sender, PlatformEvent platformEvent)
    {
        ActivityFeedEntry entry = PlatformActivityFormatter.CreateEntry(platformEvent);

        lock (_syncRoot)
        {
            _entries.Insert(0, entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
        }

        if (platformEvent is ChatEvent chatEvent)
        {
            if (!_questionCandidates.Writer.TryWrite(chatEvent))
            {
                _logger.LogWarning(
                    "Dropping question detection candidate from {Platform}/{Author} because processing is not accepting new work.",
                    chatEvent.Source,
                    chatEvent.Author);
            }
        }

        EntryAdded?.Invoke(this, entry);
    }

    private async Task ProcessQuestionCandidates(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (ChatEvent chatEvent in _questionCandidates.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await QueueDetectedQuestion(chatEvent, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Failed to evaluate question candidate from {Platform}/{Author}.",
                        chatEvent.Source,
                        chatEvent.Author);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task QueueDetectedQuestion(ChatEvent chatEvent, CancellationToken cancellationToken)
    {
        if (!_chatBotOptions.Value.AutoQueueQuestions)
        {
            return;
        }

        string? questionText = await _questionLocatorService.TryLocateQuestion(chatEvent, cancellationToken);
        if (questionText is null)
        {
            return;
        }

        _questionOverlayService.TryEnqueueDetectedQuestion(
            chatEvent.Source.ToString(),
            chatEvent.Author,
            questionText,
            receivedAt: NormalizeTimestamp(chatEvent.OccurredAt));
    }

    private static DateTimeOffset NormalizeTimestamp(DateTime occurredAt) => occurredAt.Kind switch
    {
        DateTimeKind.Unspecified => new DateTimeOffset(DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc)),
        DateTimeKind.Local => new DateTimeOffset(occurredAt.ToUniversalTime(), TimeSpan.Zero),
        _ => new DateTimeOffset(occurredAt, TimeSpan.Zero)
    };
}
