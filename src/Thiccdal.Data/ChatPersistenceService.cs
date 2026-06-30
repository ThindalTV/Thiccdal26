using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Remotes;
using RuntimeChatEvent = Thiccdal.Infrastructure.Bot.Models.ChatEvent;

namespace Thiccdal.Data;

/// <summary>
/// Persists normalized chat events into platform events, chat messages, and platform users.
/// </summary>
public sealed class ChatPersistenceService : IChatPersistenceService
{
    private static readonly ActivitySource _activitySource = new ActivitySource("Thiccdal.ChatPersistence");

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<ChatPersistenceService> _logger;

    public ChatPersistenceService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<ChatPersistenceService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Persist(RuntimeChatEvent chatEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatEvent);

        if (chatEvent.PersistedRecordId > 0)
        {
            return;
        }

        using Activity? activity = _activitySource.StartActivity("ChatPersistence.Persist");
        activity?.SetTag("chat.platform", chatEvent.Source.ToString());

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        PlatformEvent persistedPlatformEvent = PlatformEventRecordFactory.Create(chatEvent, gifterPlatformUser: null);
        dbContext.PlatformEvents.Add(persistedPlatformEvent);

        string platformUserId = string.IsNullOrWhiteSpace(chatEvent.PlatformUserId)
            ? PlatformUserIdResolver.Resolve(chatEvent, _logger)
            : chatEvent.PlatformUserId;
        PlatformUser platformUser = await PlatformUserPersistenceHelper.Upsert(
            dbContext,
            chatEvent.Source,
            platformUserId,
            chatEvent.Author,
            chatEvent.OccurredAt,
            cancellationToken);
        chatEvent.PreferredAuthor = await ResolvePreferredAuthor(
            dbContext,
            platformUser,
            chatEvent.Author,
            cancellationToken);

        dbContext.ChatMessages.Add(new ChatMessage
        {
            PlatformEvent = persistedPlatformEvent,
            PlatformUser = platformUser,
            Source = chatEvent.Source,
            Content = chatEvent.Content,
            HtmlContent = chatEvent.HtmlContent,
            RawData = chatEvent.RawData,
            SentAt = chatEvent.OccurredAt
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        chatEvent.PersistedRecordId = persistedPlatformEvent.Id;

        _logger.LogInformation(
            "Persisted chat message from {Platform} user {PlatformUserId} (MessageId={MessageId})",
            chatEvent.Source,
            platformUserId,
            dbContext.Entry(dbContext.ChatMessages.Local.Single()).Entity.Id);
    }

    private static async Task<string> ResolvePreferredAuthor(
        ApplicationDbContext dbContext,
        PlatformUser platformUser,
        string fallbackAuthor,
        CancellationToken cancellationToken)
    {
        if (platformUser.UserIdentity is not null && !string.IsNullOrWhiteSpace(platformUser.UserIdentity.DisplayName))
        {
            return platformUser.UserIdentity.DisplayName.Trim();
        }

        if (platformUser.UserIdentityId.HasValue)
        {
            string? canonicalDisplayName = await dbContext.UserIdentities
                .AsNoTracking()
                .Where(identity => identity.Id == platformUser.UserIdentityId.Value)
                .Select(identity => identity.DisplayName)
                .SingleOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(canonicalDisplayName))
            {
                return canonicalDisplayName.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(platformUser.DisplayName))
        {
            return platformUser.DisplayName.Trim();
        }

        return fallbackAuthor;
    }
}
