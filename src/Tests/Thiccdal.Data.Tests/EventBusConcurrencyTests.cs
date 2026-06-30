using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Remote.Null;
using RuntimePlatformEvent = Thiccdal.Infrastructure.Bot.Models.PlatformEvent;
using RuntimePlatformEventSource = Thiccdal.Infrastructure.Bot.Models.PlatformEventSource;
using RuntimePlatformEventType = Thiccdal.Infrastructure.Bot.Models.PlatformEventType;

namespace Thiccdal.Data.Tests;

public sealed class EventBusConcurrencyTests
{
    [Fact]
    public async Task WhenMultipleEventTypesPublished_ThenEachSubscriberReceivesOnlyItsType()
    {
        string dbPath = PrepareDatabasePath(nameof(WhenMultipleEventTypesPublished_ThenEachSubscriberReceivesOnlyItsType));
        using ServiceProvider provider = BuildProvider(dbPath);
        await provider.InitializeDatabase();

        IEventBus eventBus = provider.GetRequiredService<IEventBus>();
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Channel<RuntimePlatformEvent> followReceived = Channel.CreateUnbounded<RuntimePlatformEvent>();
        Channel<RuntimePlatformEvent> subscribeReceived = Channel.CreateUnbounded<RuntimePlatformEvent>();

        // Subscriber A: only passes Follow events downstream
        Task followSubscriberTask = Task.Run(async () =>
        {
            await foreach (RuntimePlatformEvent evt in eventBus.Subscribe(cts.Token))
            {
                if (evt.Type == RuntimePlatformEventType.Follow)
                {
                    await followReceived.Writer.WriteAsync(evt, cts.Token);
                }
            }
            followReceived.Writer.TryComplete();
        });

        // Subscriber B: only passes Subscribe events downstream
        Task subscribeSubscriberTask = Task.Run(async () =>
        {
            await foreach (RuntimePlatformEvent evt in eventBus.Subscribe(cts.Token))
            {
                if (evt.Type == RuntimePlatformEventType.Subscribe)
                {
                    await subscribeReceived.Writer.WriteAsync(evt, cts.Token);
                }
            }
            subscribeReceived.Writer.TryComplete();
        });

        await Task.Delay(50, cts.Token);

        await eventBus.Publish(BuildEvent(RuntimePlatformEventType.Follow, "follow-1"), cts.Token);
        await eventBus.Publish(BuildEvent(RuntimePlatformEventType.Subscribe, "sub-1"), cts.Token);

        RuntimePlatformEvent receivedFollow = await followReceived.Reader.ReadAsync(cts.Token);
        RuntimePlatformEvent receivedSubscribe = await subscribeReceived.Reader.ReadAsync(cts.Token);

        cts.Cancel();
        await Task.WhenAny(Task.WhenAll(followSubscriberTask, subscribeSubscriberTask), Task.Delay(1000));

        Assert.Equal(RuntimePlatformEventType.Follow, receivedFollow.Type);
        Assert.Equal("follow-1", receivedFollow.ExternalId);
        Assert.Equal(RuntimePlatformEventType.Subscribe, receivedSubscribe.Type);
        Assert.Equal("sub-1", receivedSubscribe.ExternalId);
    }

    [Fact]
    public async Task WhenOneSubscriberThrows_ThenOtherSubscribersStillReceiveEvents()
    {
        string dbPath = PrepareDatabasePath(nameof(WhenOneSubscriberThrows_ThenOtherSubscribersStillReceiveEvents));
        using ServiceProvider provider = BuildProvider(dbPath);
        await provider.InitializeDatabase();

        IEventBus eventBus = provider.GetRequiredService<IEventBus>();
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Channel<RuntimePlatformEvent> goodReceived = Channel.CreateUnbounded<RuntimePlatformEvent>();

        // Throwing subscriber — processes one event then throws; the EventBus channel is still writable
        Task throwingTask = Task.Run(async () =>
        {
            await foreach (RuntimePlatformEvent evt in eventBus.Subscribe(cts.Token))
            {
                _ = evt;
                throw new InvalidOperationException("Subscriber fault");
            }
        });

        // Good subscriber — collects events normally
        Task goodTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RuntimePlatformEvent evt in eventBus.Subscribe(cts.Token))
                {
                    await goodReceived.Writer.WriteAsync(evt, cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                goodReceived.Writer.TryComplete();
            }
        });

        await Task.Delay(50, cts.Token);

        await eventBus.Publish(BuildEvent(RuntimePlatformEventType.Raw, "fault-test-1"), cts.Token);

        RuntimePlatformEvent delivered = await goodReceived.Reader.ReadAsync(cts.Token);

        cts.Cancel();
        await Task.WhenAny(goodTask, Task.Delay(1000));

        Assert.Equal("fault-test-1", delivered.ExternalId);
    }

    [Fact]
    public async Task WhenEventsPublishedConcurrently_ThenAllAreDelivered()
    {
        const int eventCount = 50;

        string dbPath = PrepareDatabasePath(nameof(WhenEventsPublishedConcurrently_ThenAllAreDelivered));
        using ServiceProvider provider = BuildProvider(dbPath);
        await provider.InitializeDatabase();

        IEventBus eventBus = provider.GetRequiredService<IEventBus>();
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        int deliveredCount = 0;
        TaskCompletionSource allDeliveredTcs = new TaskCompletionSource();

        Task subscriberTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RuntimePlatformEvent evt in eventBus.Subscribe(cts.Token))
                {
                    _ = evt;
                    if (Interlocked.Increment(ref deliveredCount) == eventCount)
                    {
                        allDeliveredTcs.TrySetResult();
                    }
                }
            }
            catch (OperationCanceledException) { }
        });

        await Task.Delay(50, cts.Token);

        RuntimePlatformEvent[] events = Enumerable.Range(1, eventCount)
            .Select(i => BuildEvent(RuntimePlatformEventType.Raw, $"concurrent-{i}"))
            .ToArray();

        await Task.WhenAll(events.Select(e => eventBus.Publish(e, cts.Token)));

        bool allDelivered = await Task.WhenAny(allDeliveredTcs.Task, Task.Delay(TimeSpan.FromSeconds(10))) == allDeliveredTcs.Task;

        cts.Cancel();
        await Task.WhenAny(subscriberTask, Task.Delay(1000));

        Assert.True(allDelivered, $"Expected all {eventCount} events to be delivered, got {deliveredCount}");
        Assert.Equal(eventCount, deliveredCount);
    }

    [Fact]
    public async Task WhenSubscriberUnsubscribes_ThenNoFurtherEventsReceived()
    {
        string dbPath = PrepareDatabasePath(nameof(WhenSubscriberUnsubscribes_ThenNoFurtherEventsReceived));
        using ServiceProvider provider = BuildProvider(dbPath);
        await provider.InitializeDatabase();

        IEventBus eventBus = provider.GetRequiredService<IEventBus>();
        using CancellationTokenSource publishCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using CancellationTokenSource subscriberCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Channel<RuntimePlatformEvent> received = Channel.CreateUnbounded<RuntimePlatformEvent>();

        Task subscriberTask = Task.Run(async () =>
        {
            try
            {
                await foreach (RuntimePlatformEvent evt in eventBus.Subscribe(subscriberCts.Token))
                {
                    await received.Writer.WriteAsync(evt, publishCts.Token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                received.Writer.TryComplete();
            }
        });

        await Task.Delay(50, publishCts.Token);

        await eventBus.Publish(BuildEvent(RuntimePlatformEventType.Raw, "before-unsub"), publishCts.Token);

        RuntimePlatformEvent firstEvent = await received.Reader.ReadAsync(publishCts.Token);
        Assert.Equal("before-unsub", firstEvent.ExternalId);

        // Cancel subscription — no further events should reach the subscriber
        subscriberCts.Cancel();
        await Task.WhenAny(subscriberTask, Task.Delay(1000));

        await eventBus.Publish(BuildEvent(RuntimePlatformEventType.Raw, "after-unsub-1"), publishCts.Token);
        await eventBus.Publish(BuildEvent(RuntimePlatformEventType.Raw, "after-unsub-2"), publishCts.Token);

        await Task.Delay(100, publishCts.Token);

        Assert.False(received.Reader.TryRead(out _), "Unsubscribed subscriber should not receive further events");
    }

    private static RuntimePlatformEvent BuildEvent(RuntimePlatformEventType type, string externalId) =>
        new RuntimePlatformEvent
        {
            Source = RuntimePlatformEventSource.Null,
            Type = type,
            Author = "test",
            Channel = "test",
            ExternalId = externalId,
            Summary = $"Test {type} event",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"platform\":\"null\"}"
        };

    private static ServiceProvider BuildProvider(string databasePath)
    {
        ConfigurationManager configuration = new ConfigurationManager();
        configuration[$"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.DefaultConnection)}"] =
            $"Data Source={databasePath}";
        configuration[$"{NullOptions.SectionName}:PlatformName"] = "Offline";
        configuration[$"{NullOptions.SectionName}:AuthorizationUrl"] = "https://example.test/null";

        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddThiccdalData(configuration);
        return services.BuildServiceProvider();
    }

    private static string PrepareDatabasePath(string testName)
    {
        string rootPath = Path.Combine(AppContext.BaseDirectory, "EventBusConcurrencyTests", testName);
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }

        Directory.CreateDirectory(rootPath);
        return Path.Combine(rootPath, "thiccdal.db");
    }
}
