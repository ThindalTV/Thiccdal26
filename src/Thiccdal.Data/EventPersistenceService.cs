using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Remotes;
using RuntimeChatEvent = Thiccdal.Infrastructure.Bot.Models.ChatEvent;
using RuntimePlatformEvent = Thiccdal.Infrastructure.Bot.Models.PlatformEvent;
using RuntimeSubscribeEvent = Thiccdal.Infrastructure.Bot.Models.TwitchSubscribeEvent;

namespace Thiccdal.Data;

/// <summary>
/// Persists normalized platform events into the current database shape.
/// </summary>
public sealed class EventPersistenceService : IEventPersistenceService
{
    private static readonly ActivitySource _activitySource = new ActivitySource("Thiccdal.EventPersistence");

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<EventPersistenceService> _logger;

    public EventPersistenceService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<EventPersistenceService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Persist(RuntimePlatformEvent platformEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(platformEvent);

        if (platformEvent.PersistedRecordId > 0)
        {
            return;
        }

        using Activity? activity = _activitySource.StartActivity("EventPersistence.Persist");
        activity?.SetTag("event.type", platformEvent.Type.ToString());
        activity?.SetTag("event.source", platformEvent.Source.ToString());

        if (platformEvent is RuntimeChatEvent chatEvent)
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IChatPersistenceService chatPersistenceService = scope.ServiceProvider
                .GetRequiredService<IChatPersistenceService>();
            await chatPersistenceService.Persist(chatEvent, cancellationToken);
            return;
        }

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        PlatformUser? gifterPlatformUser = await GetOrCreateGifterPlatformUser(dbContext, platformEvent, cancellationToken);
        PlatformEvent persistedPlatformEvent = PlatformEventRecordFactory.Create(platformEvent, gifterPlatformUser);

        dbContext.PlatformEvents.Add(persistedPlatformEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        platformEvent.PersistedRecordId = persistedPlatformEvent.Id;

        _logger.LogInformation(
            "Persisted {EventType} from {Platform} (Id={Id})",
            platformEvent.Type,
            platformEvent.Source,
            persistedPlatformEvent.Id);
    }

    private static async Task<PlatformUser?> GetOrCreateGifterPlatformUser(
        ApplicationDbContext dbContext,
        RuntimePlatformEvent platformEvent,
        CancellationToken cancellationToken)
    {
        if (platformEvent is not RuntimeSubscribeEvent subscribeEvent ||
            string.IsNullOrWhiteSpace(subscribeEvent.GifterUserId))
        {
            return null;
        }

        return await PlatformUserPersistenceHelper.Upsert(
            dbContext,
            subscribeEvent.Source,
            subscribeEvent.GifterUserId,
            subscribeEvent.GifterUserId,
            subscribeEvent.OccurredAt,
            cancellationToken);
    }
}
