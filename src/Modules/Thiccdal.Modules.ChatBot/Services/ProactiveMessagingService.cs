using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;

namespace Thiccdal.Modules.ChatBot.Services;

public sealed class ProactiveMessagingService : IProactiveMessagingService, IHostedService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IProactiveMessageCatalog _proactiveMessageCatalog;
    private readonly IChatService _chatService;
    private readonly ILogger<ProactiveMessagingService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lifecycleLock = new();

    private CancellationTokenSource? _loopCancellationTokenSource;
    private Task? _loopTask;

    public ProactiveMessagingService(
        IProactiveMessageCatalog proactiveMessageCatalog,
        IChatService chatService,
        ILogger<ProactiveMessagingService> logger,
        TimeProvider? timeProvider = null)
    {
        _proactiveMessageCatalog = proactiveMessageCatalog;
        _chatService = chatService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lifecycleLock)
        {
            if (_loopTask is not null)
            {
                return Task.CompletedTask;
            }

            _loopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = RunLoop(_loopCancellationTokenSource.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? loopTask;
        CancellationTokenSource? loopCancellationTokenSource;

        lock (_lifecycleLock)
        {
            loopTask = _loopTask;
            loopCancellationTokenSource = _loopCancellationTokenSource;
            _loopCancellationTokenSource = null;
            _loopTask = null;
        }

        if (loopTask is null || loopCancellationTokenSource is null)
        {
            return;
        }

        await loopCancellationTokenSource.CancelAsync();

        try
        {
            await loopTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            loopCancellationTokenSource.Dispose();
        }
    }

    public async Task ExecuteDueMessages(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProactiveMessageDefinition> messages = await _proactiveMessageCatalog.GetEnabledMessages(cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        foreach (ProactiveMessageDefinition message in messages.Where(candidate => IsDue(candidate, now)))
        {
            try
            {
                await _chatService.SendMessage(message.Message, cancellationToken);
                await _proactiveMessageCatalog.MarkSent(message.Id, now, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to send proactive message {ProactiveMessageId}.", message.Id);
            }
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? loopCancellationTokenSource;

        lock (_lifecycleLock)
        {
            loopCancellationTokenSource = _loopCancellationTokenSource;
            _loopCancellationTokenSource = null;
            _loopTask = null;
        }

        loopCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task RunLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await ExecuteDueMessages(cancellationToken);
                await Task.Delay(PollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static bool IsDue(ProactiveMessageDefinition message, DateTimeOffset now)
    {
        if (message.IntervalSeconds <= 0)
        {
            return false;
        }

        return !message.LastSentAt.HasValue || now - message.LastSentAt.Value >= TimeSpan.FromSeconds(message.IntervalSeconds);
    }
}
