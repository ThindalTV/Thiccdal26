using System.Threading.Channels;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Data;

/// <summary>
/// Forwards platform connection events into the shared event bus.
/// </summary>
public sealed class PlatformEventPump : IPlatformEventPump
{
    private readonly IEventBus _eventBus;

    public PlatformEventPump(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public async Task Run(IPlatformConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Channel<PlatformEvent> channel = Channel.CreateUnbounded<PlatformEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        void HandlePlatformEventReceived(object? sender, PlatformEvent platformEvent)
        {
            channel.Writer.TryWrite(platformEvent);
        }

        connection.OnPlatformEventReceived += HandlePlatformEventReceived;

        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((Channel<PlatformEvent>)state!).Writer.TryComplete(),
            channel);

        try
        {
            await foreach (PlatformEvent platformEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await _eventBus.Publish(platformEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            connection.OnPlatformEventReceived -= HandlePlatformEventReceived;
            channel.Writer.TryComplete();
        }
    }
}
