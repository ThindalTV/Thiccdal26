using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class ProactiveMessagingServiceTests
{
    [Fact]
    public async Task WhenMessageIsDue_ThenItIsSentAndMarked()
    {
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 05, 29, 12, 00, 00, TimeSpan.Zero));
        StubProactiveMessageCatalog catalog = new(
        [
            new ProactiveMessageDefinition(1, "Stay hydrated", 60, true, timeProvider.GetUtcNow().AddMinutes(-2))
        ]);
        RecordingChatService chatService = new();
        ProactiveMessagingService service = new(catalog, chatService, new RecordingLogger<ProactiveMessagingService>(), timeProvider);

        await service.ExecuteDueMessages();

        Assert.Equal(["Stay hydrated"], chatService.Messages);
        Assert.Single(catalog.MarkedMessages);
        Assert.Equal(1L, catalog.MarkedMessages[0].MessageId);
    }

    [Fact]
    public async Task WhenMessageIsNotDue_ThenNothingIsSent()
    {
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 05, 29, 12, 00, 00, TimeSpan.Zero));
        StubProactiveMessageCatalog catalog = new(
        [
            new ProactiveMessageDefinition(1, "Stay hydrated", 300, true, timeProvider.GetUtcNow().AddMinutes(-2))
        ]);
        RecordingChatService chatService = new();
        ProactiveMessagingService service = new(catalog, chatService, new RecordingLogger<ProactiveMessagingService>(), timeProvider);

        await service.ExecuteDueMessages();

        Assert.Empty(chatService.Messages);
        Assert.Empty(catalog.MarkedMessages);
    }

    private sealed class StubProactiveMessageCatalog : IProactiveMessageCatalog
    {
        private readonly IReadOnlyList<ProactiveMessageDefinition> _messages;

        public StubProactiveMessageCatalog(IReadOnlyList<ProactiveMessageDefinition> messages)
        {
            _messages = messages;
        }

        public List<(long MessageId, DateTimeOffset SentAt)> MarkedMessages { get; } = [];

        public Task<IReadOnlyList<ProactiveMessageDefinition>> GetEnabledMessages(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_messages);
        }

        public Task MarkSent(long messageId, DateTimeOffset sentAt, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MarkedMessages.Add((messageId, sentAt));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingChatService : IChatService
    {
        public bool Connected { get; private set; }

        public event EventHandler<ChatEvent>? OnChatMessageReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived
        {
            add { }
            remove { }
        }

        public List<string> Messages { get; } = [];

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task Connect(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Connected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Connected = false;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
