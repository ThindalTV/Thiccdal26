using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Modules.ChatBot.Services;

/// <summary>
/// Aggregates chat across all registered platform connections and fans messages out to independent subscribers.
/// </summary>
public sealed class ChatAggregationService : IChatService, IChatAggregationService, IHostedService, IDisposable
{
    private const int SubscriberBufferCapacity = 512;

    private readonly IReadOnlyList<IPlatformConnection> _platformConnections;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ILogger<ChatAggregationService> _logger;
    private readonly ConcurrentDictionary<Guid, Channel<ChatEvent>> _subscribers = new();
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private CancellationTokenSource? _connectionLifetimeCancellationTokenSource;
    private List<Task> _connectionLifetimeTasks = [];
    private bool _connected;
    private bool _disposed;

    public ChatAggregationService(
        IEnumerable<IPlatformConnection> platformConnections,
        IServiceScopeFactory serviceScopeFactory,
        ICommandDispatcher commandDispatcher,
        ILogger<ChatAggregationService> logger)
    {
        _platformConnections = platformConnections.ToList();
        _serviceScopeFactory = serviceScopeFactory;
        _commandDispatcher = commandDispatcher;
        _logger = logger;

        foreach (IPlatformConnection platformConnection in _platformConnections)
        {
            platformConnection.OnPlatformEventReceived += HandlePlatformEventReceived;
        }
    }

    public event EventHandler<ChatEvent>? OnChatMessageReceived;

    public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Connect(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Disconnect(cancellationToken);
    }

    public async Task Connect(CancellationToken ct)
    {
        await _lifecycleLock.WaitAsync(ct);

        try
        {
            if (_connected)
            {
                return;
            }

            // The lifetime CTS must NOT be linked to the caller token: the caller's token only governs the
            // connection attempt itself and may be short-lived (e.g., a request token). Linking it would
            // disconnect all platforms when the caller cancels.
            CancellationTokenSource lifetimeCancellationTokenSource = new CancellationTokenSource();
            List<Task> lifetimeTasks = [];

            foreach (IPlatformConnection platformConnection in _platformConnections)
            {
                if (!platformConnection.Connected)
                {
                    await platformConnection.Connect(ct);
                }

                lifetimeTasks.Add(RunConnectionLifetime(platformConnection, lifetimeCancellationTokenSource.Token));
            }

            _connectionLifetimeCancellationTokenSource = lifetimeCancellationTokenSource;
            _connectionLifetimeTasks = lifetimeTasks;
            _connected = true;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task Disconnect(CancellationToken ct)
    {
        CancellationTokenSource? lifetimeCancellationTokenSource;
        IReadOnlyList<Task> lifetimeTasks;

        await _lifecycleLock.WaitAsync(ct);

        try
        {
            if (!_connected)
            {
                return;
            }

            _connected = false;
            lifetimeCancellationTokenSource = _connectionLifetimeCancellationTokenSource;
            lifetimeTasks = _connectionLifetimeTasks.ToArray();
            _connectionLifetimeCancellationTokenSource = null;
            _connectionLifetimeTasks = [];
        }
        finally
        {
            _lifecycleLock.Release();
        }

        if (lifetimeCancellationTokenSource is not null)
        {
            await lifetimeCancellationTokenSource.CancelAsync();
            lifetimeCancellationTokenSource.Dispose();
        }

        await Task.WhenAll(lifetimeTasks);
    }

    public async Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        foreach (IPlatformConnection platformConnection in _platformConnections.Where(connection => connection.Connected))
        {
            await platformConnection.SendMessage(message, cancellationToken);
        }
    }

    public async IAsyncEnumerable<ChatEvent> Subscribe(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guid subscriberId = Guid.NewGuid();
        Channel<ChatEvent> channel = CreateSubscriberChannel(subscriberId);

        if (!_subscribers.TryAdd(subscriberId, channel))
        {
            throw new InvalidOperationException("Could not register chat aggregation subscriber.");
        }

        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((Channel<ChatEvent>)state!).Writer.TryComplete(),
            channel);

        try
        {
            await foreach (ChatEvent chatEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return chatEvent;
            }
        }
        finally
        {
            RemoveSubscriber(subscriberId, channel);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (IPlatformConnection platformConnection in _platformConnections)
        {
            platformConnection.OnPlatformEventReceived -= HandlePlatformEventReceived;
        }

        foreach (KeyValuePair<Guid, Channel<ChatEvent>> subscriber in _subscribers)
        {
            RemoveSubscriber(subscriber.Key, subscriber.Value);
        }

        _connectionLifetimeCancellationTokenSource?.Dispose();
        _lifecycleLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static async Task RunConnectionLifetime(IPlatformConnection platformConnection, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (platformConnection.Connected)
            {
                await platformConnection.Disconnect(CancellationToken.None);
            }
        }
    }

    private Channel<ChatEvent> CreateSubscriberChannel(Guid subscriberId)
    {
        return Channel.CreateBounded<ChatEvent>(
            new BoundedChannelOptions(SubscriberBufferCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            },
            droppedEvent =>
            {
                _logger.LogWarning(
                    "Dropping oldest aggregated chat event for subscriber {SubscriberId} after reaching capacity {Capacity}. Dropped {Platform}/{ExternalId}.",
                    subscriberId,
                    SubscriberBufferCapacity,
                    droppedEvent.Source,
                    droppedEvent.ExternalId);
            });
    }

    private void HandlePlatformEventReceived(object? sender, PlatformEvent platformEvent)
    {
        _ = PersistAndDispatchPlatformEvent(platformEvent);
    }

    private async Task PersistAndDispatchPlatformEvent(PlatformEvent platformEvent)
    {
        try
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();

            if (platformEvent is ChatEvent chatEvent)
            {
                if (chatEvent.PersistedRecordId <= 0)
                {
                    IChatPersistenceService chatPersistenceService = scope.ServiceProvider
                        .GetRequiredService<IChatPersistenceService>();
                    await chatPersistenceService.Persist(chatEvent);
                }

                OnPlatformEventReceived?.Invoke(this, chatEvent);
                OnChatMessageReceived?.Invoke(this, chatEvent);
                DispatchChatEvent(chatEvent);
                await _commandDispatcher.Dispatch(chatEvent);
                return;
            }

            if (platformEvent.PersistedRecordId <= 0)
            {
                IEventPersistenceService eventPersistenceService = scope.ServiceProvider
                    .GetRequiredService<IEventPersistenceService>();
                await eventPersistenceService.Persist(platformEvent);
            }

            OnPlatformEventReceived?.Invoke(this, platformEvent);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Failed to persist and dispatch platform event {Platform} / {EventType}",
                platformEvent.Source,
                platformEvent.Type);
        }
    }

    private void DispatchChatEvent(ChatEvent chatEvent)
    {
        foreach (KeyValuePair<Guid, Channel<ChatEvent>> subscriber in _subscribers)
        {
            if (!subscriber.Value.Writer.TryWrite(chatEvent))
            {
                RemoveSubscriber(subscriber.Key, subscriber.Value);
            }
        }
    }

    private void RemoveSubscriber(Guid subscriberId, Channel<ChatEvent> channel)
    {
        if (_subscribers.TryRemove(subscriberId, out _))
        {
            channel.Writer.TryComplete();
        }
    }
}
