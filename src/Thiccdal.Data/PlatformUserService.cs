using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Data;

/// <summary>
/// Creates and updates persisted platform users.
/// </summary>
public sealed class PlatformUserService : IPlatformUserService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<PlatformUserService> _logger;
    private readonly UserIdentityOptions _options;

    public PlatformUserService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<PlatformUserService> logger,
        IOptions<UserIdentityOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<long> Upsert(
        PlatformEventSource source,
        string platformUserId,
        string displayName,
        DateTime lastSeen,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        bool alreadyTracked = await dbContext.PlatformUsers.AnyAsync(
            user => user.Source == source && user.PlatformUserId == platformUserId,
            cancellationToken);

        Data.Models.PlatformUser platformUser = await PlatformUserPersistenceHelper.Upsert(
            dbContext,
            source,
            platformUserId,
            displayName,
            lastSeen,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        int createdSuggestionCount = 0;
        if (!alreadyTracked)
        {
            createdSuggestionCount = await CreateSuggestions(dbContext, platformUser, cancellationToken);

            if (createdSuggestionCount > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        _logger.LogInformation(
            alreadyTracked
                ? "Updated platform user {Platform}/{PlatformUserId}"
                : "Created platform user {Platform}/{PlatformUserId}",
            source,
            platformUserId);

        if (createdSuggestionCount > 0)
        {
            _logger.LogInformation(
                "Created {SuggestionCount} user identity suggestion(s) for {Platform}/{PlatformUserId}",
                createdSuggestionCount,
                source,
                platformUserId);
        }

        return platformUser.Id;
    }

    private async Task<int> CreateSuggestions(
        ApplicationDbContext dbContext,
        PlatformUser platformUser,
        CancellationToken cancellationToken)
    {
        if (_options.SimilarityThreshold <= 0d)
        {
            return 0;
        }

        List<PlatformUser> candidates = await dbContext.PlatformUsers
            .Where(candidate => candidate.Id != platformUser.Id && candidate.Source != platformUser.Source)
            .OrderBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken);

        int createdCount = 0;
        foreach (PlatformUser candidate in candidates)
        {
            double similarity = UserIdentitySuggestionMatcher.CalculateSimilarity(
                platformUser.DisplayName,
                candidate.DisplayName);

            if (similarity < _options.SimilarityThreshold)
            {
                continue;
            }

            long firstPlatformUserId = Math.Min(platformUser.Id, candidate.Id);
            long secondPlatformUserId = Math.Max(platformUser.Id, candidate.Id);

            dbContext.UserIdentitySuggestions.Add(new UserIdentitySuggestion
            {
                FirstPlatformUserId = firstPlatformUserId,
                SecondPlatformUserId = secondPlatformUserId,
                SimilarityScore = similarity
            });

            createdCount++;
        }

        return createdCount;
    }
}
