using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Remotes;
using PersistedPlatformEvent = Thiccdal.Data.Models.PlatformEvent;
using Thiccdal.Remote.Null;
using RuntimePlatformEvent = Thiccdal.Infrastructure.Bot.Models.PlatformEvent;
using RuntimePlatformEventSource = Thiccdal.Infrastructure.Bot.Models.PlatformEventSource;
using RuntimePlatformEventType = Thiccdal.Infrastructure.Bot.Models.PlatformEventType;

namespace Thiccdal.Data.Tests;

public sealed class EventBusTests
{
    [Fact]
    public async Task WhenPublishingEvent_ThenItIsPersistedBeforeSubscribersReceiveIt()
    {
        string databasePath = PrepareDatabasePath(nameof(WhenPublishingEvent_ThenItIsPersistedBeforeSubscribersReceiveIt));
        ConfigurationManager configuration = BuildConfiguration(databasePath);
        ServiceCollection services = new();
        services.AddLogging();
        services.AddThiccdalData(configuration);
        services.AddNullIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitializeDatabase();

        _ = provider.GetRequiredService<IPlatformConnection>();

        IEventBus eventBus = provider.GetRequiredService<IEventBus>();
        using CancellationTokenSource subscriptionCancellation = new(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<RuntimePlatformEvent> subscriber = eventBus.Subscribe(subscriptionCancellation.Token)
            .GetAsyncEnumerator(subscriptionCancellation.Token);
        Task<bool> moveNextTask = subscriber.MoveNextAsync().AsTask();
        RuntimePlatformEvent platformEvent = new()
        {
            Source = RuntimePlatformEventSource.Null,
            Type = RuntimePlatformEventType.Raw,
            Author = "offline",
            Channel = "offline",
            ExternalId = "null-raw-1",
            Summary = "Null platform event",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"platform\":\"null\"}"
        };

        await eventBus.Publish(platformEvent);

        Assert.True(await moveNextTask);
        Assert.Same(platformEvent, subscriber.Current);
        Assert.True(subscriber.Current.PersistedRecordId > 0);

        await using ApplicationDbContext dbContext = await provider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();
        PersistedPlatformEvent storedEvent = await dbContext.PlatformEvents.SingleAsync();
        Assert.Equal(platformEvent.PersistedRecordId, storedEvent.Id);
        Assert.Equal("null-raw-1", storedEvent.ExternalId);
    }

    [Fact]
    public async Task WhenSubscriberIsCancelled_ThenOtherSubscribersStillReceivePublishedEvents()
    {
        string databasePath = PrepareDatabasePath(nameof(WhenSubscriberIsCancelled_ThenOtherSubscribersStillReceivePublishedEvents));
        ConfigurationManager configuration = BuildConfiguration(databasePath);
        ServiceCollection services = new();
        services.AddLogging();
        services.AddThiccdalData(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitializeDatabase();

        IEventBus eventBus = provider.GetRequiredService<IEventBus>();
        using CancellationTokenSource firstCancellation = new();
        using CancellationTokenSource secondCancellation = new(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<RuntimePlatformEvent> cancelledSubscriber = eventBus.Subscribe(firstCancellation.Token)
            .GetAsyncEnumerator(firstCancellation.Token);
        await using IAsyncEnumerator<RuntimePlatformEvent> activeSubscriber = eventBus.Subscribe(secondCancellation.Token)
            .GetAsyncEnumerator(secondCancellation.Token);

        Task<bool> cancelledMoveNextTask = cancelledSubscriber.MoveNextAsync().AsTask();
        Task<bool> activeMoveNextTask = activeSubscriber.MoveNextAsync().AsTask();

        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelledMoveNextTask);

        RuntimePlatformEvent platformEvent = new()
        {
            Source = RuntimePlatformEventSource.Null,
            Type = RuntimePlatformEventType.Raw,
            Author = "offline",
            Channel = "offline",
            ExternalId = "null-raw-2",
            Summary = "Still delivered",
            OccurredAt = DateTime.UtcNow,
            RawData = "{\"platform\":\"null\",\"sequence\":2}"
        };

        await eventBus.Publish(platformEvent);

        Assert.True(await activeMoveNextTask);
        Assert.Equal("null-raw-2", activeSubscriber.Current.ExternalId);
    }

    private static ConfigurationManager BuildConfiguration(string databasePath)
    {
        ConfigurationManager configuration = new();
        configuration[$"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.DefaultConnection)}"] =
            $"Data Source={databasePath}";
        configuration[$"{NullOptions.SectionName}:PlatformName"] = "Offline";
        configuration[$"{NullOptions.SectionName}:AuthorizationUrl"] = "https://example.test/null";
        return configuration;
    }

    private static string PrepareDatabasePath(string testName)
    {
        string rootPath = Path.Combine(AppContext.BaseDirectory, "EventBusTests", testName);
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }

        Directory.CreateDirectory(rootPath);
        return Path.Combine(rootPath, "thiccdal.db");
    }
}
