using System.Threading.Channels;
using System.Runtime.CompilerServices;
using Thiccdal.Infrastructure.Remotes;
using RuntimeChatEvent = Thiccdal.Infrastructure.Bot.Models.ChatEvent;
using RuntimePlatformEvent = Thiccdal.Infrastructure.Bot.Models.PlatformEvent;
using RuntimePlatformEventSource = Thiccdal.Infrastructure.Bot.Models.PlatformEventSource;
using RuntimePlatformEventType = Thiccdal.Infrastructure.Bot.Models.PlatformEventType;

namespace Thiccdal.Data.Tests;

public sealed class PlatformEventPumpTests
{
    [Fact]
    public async Task WhenPlatformConnectionRaisesEvent_ThenPumpPublishesIt()
    {
        var eventBus = new RecordingEventBus();
        var pump = new PlatformEventPump(eventBus);
        var connection = new FakePlatformConnection();
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(5));

        Task runTask = pump.Run(connection, cancellationTokenSource.Token);
        RuntimePlatformEvent platformEvent = new()
        {
            Source = RuntimePlatformEventSource.Null,
            Type = RuntimePlatformEventType.Raw,
            Author = "system",
            Channel = "offline",
            ExternalId = "pump-1",
            Summary = "Pumped event",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"pump\":true}"
        };

        connection.Emit(platformEvent);

        RuntimePlatformEvent publishedEvent = await eventBus.ReadPublishedEvent(cancellationTokenSource.Token);
        Assert.Same(platformEvent, publishedEvent);

        cancellationTokenSource.Cancel();
        await runTask;
    }

    private sealed class RecordingEventBus : IEventBus
    {
        private readonly Channel<RuntimePlatformEvent> _publishedEvents = Channel.CreateUnbounded<RuntimePlatformEvent>();

        public Task Publish(RuntimePlatformEvent platformEvent, CancellationToken cancellationToken = default)
        {
            _publishedEvents.Writer.TryWrite(platformEvent);
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<RuntimePlatformEvent> Subscribe(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (RuntimePlatformEvent platformEvent in _publishedEvents.Reader.ReadAllAsync(cancellationToken))
            {
                yield return platformEvent;
            }
        }

        public ValueTask<RuntimePlatformEvent> ReadPublishedEvent(CancellationToken cancellationToken)
        {
            return _publishedEvents.Reader.ReadAsync(cancellationToken);
        }
    }

    private sealed class FakePlatformConnection : IPlatformConnection
    {
        public string PlatformName => "Fake";

        public PlatformConnectionState State => Connected
            ? PlatformConnectionState.Connected
            : PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool Connected { get; private set; }

        public event EventHandler<RuntimeChatEvent>? OnChatMessageRecieved;
        public event EventHandler<RuntimePlatformEvent>? OnPlatformEventReceived;

        public Task Connect(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connected = false;
            return Task.CompletedTask;
        }

        public void Emit(RuntimePlatformEvent platformEvent)
        {
            OnPlatformEventReceived?.Invoke(this, platformEvent);
            if (platformEvent is RuntimeChatEvent chatEvent)
            {
                OnChatMessageRecieved?.Invoke(this, chatEvent);
            }
        }

        public string GetAuthorizationUrl()
        {
            return string.Empty;
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
