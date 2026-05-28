using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Data;

/// <summary>
/// Broadcasts persisted platform events to in-process subscribers.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Guid, Channel<PlatformEvent>> _subscribers = new();
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EventBus(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc />
    public async Task Publish(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(platformEvent);

        using IServiceScope scope = _serviceScopeFactory.CreateScope();
        IEventPersistenceService persistenceService = scope.ServiceProvider.GetRequiredService<IEventPersistenceService>();
        await persistenceService.Persist(platformEvent, cancellationToken);

        foreach (KeyValuePair<Guid, Channel<PlatformEvent>> subscriber in _subscribers)
        {
            if (!subscriber.Value.Writer.TryWrite(platformEvent))
            {
                RemoveSubscriber(subscriber.Key, subscriber.Value);
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PlatformEvent> Subscribe(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guid subscriberId = Guid.NewGuid();
        Channel<PlatformEvent> channel = Channel.CreateUnbounded<PlatformEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        if (!_subscribers.TryAdd(subscriberId, channel))
        {
            throw new InvalidOperationException("Could not register platform event subscriber.");
        }

        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((Channel<PlatformEvent>)state!).Writer.TryComplete(),
            channel);

        try
        {
            await foreach (PlatformEvent platformEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return platformEvent;
            }
        }
        finally
        {
            RemoveSubscriber(subscriberId, channel);
        }
    }

    private void RemoveSubscriber(Guid subscriberId, Channel<PlatformEvent> channel)
    {
        if (_subscribers.TryRemove(subscriberId, out _))
        {
            channel.Writer.TryComplete();
        }
    }
}
