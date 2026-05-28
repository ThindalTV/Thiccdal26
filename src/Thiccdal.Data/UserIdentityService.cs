using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Remotes.Models;

namespace Thiccdal.Data;

/// <summary>
/// Provides manual operator workflows for reviewing and merging platform-user identities.
/// </summary>
public sealed class UserIdentityService : IUserIdentityService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public UserIdentityService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<UserIdentitySearchResult>> Search(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<UserIdentitySearchResult>();
        }

        string trimmedQuery = query.Trim();
        string likePattern = $"%{trimmedQuery}%";

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        UserIdentitySearchResult[] results = await dbContext.PlatformUsers
            .AsNoTracking()
            .Where(platformUser =>
                EF.Functions.Like(platformUser.DisplayName, likePattern) ||
                (platformUser.UserIdentity != null && EF.Functions.Like(platformUser.UserIdentity.DisplayName, likePattern)))
            .OrderBy(platformUser => platformUser.DisplayName)
            .ThenBy(platformUser => platformUser.Source)
            .ThenBy(platformUser => platformUser.PlatformUserId)
            .Take(50)
            .Select(
                platformUser => new UserIdentitySearchResult(
                    platformUser.Id,
                    platformUser.Source,
                    platformUser.PlatformUserId,
                    platformUser.DisplayName,
                    platformUser.LastSeen,
                    platformUser.UserIdentityId,
                    platformUser.UserIdentity != null ? platformUser.UserIdentity.DisplayName : null))
            .ToArrayAsync(cancellationToken);

        return results;
    }

    public Task<UserIdentityMergeResult> Merge(
        IReadOnlyList<long> platformUserIds,
        string? canonicalName,
        CancellationToken cancellationToken = default)
    {
        return Merge(new UserIdentityMergeRequest(platformUserIds, null, canonicalName), cancellationToken);
    }

    public async Task<UserIdentityMergeResult> Merge(
        UserIdentityMergeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        long[] selectedIds = request.PlatformUserIds
            .Where(static id => id > 0)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

        if (selectedIds.Length < 2)
        {
            throw new InvalidOperationException("Select at least two viewer records before merging.");
        }

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool useTransaction = dbContext.Database.IsRelational();
        await using IDbContextTransaction? transaction = useTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        List<PlatformUser> selectedUsers = await dbContext.PlatformUsers
            .Where(platformUser => selectedIds.Contains(platformUser.Id))
            .OrderBy(platformUser => platformUser.Id)
            .ToListAsync(cancellationToken);

        if (selectedUsers.Count != selectedIds.Length)
        {
            throw new InvalidOperationException("One or more selected viewer records no longer exist.");
        }

        PlatformUser? targetPlatformUser = null;
        if (request.TargetPlatformUserId.HasValue)
        {
            targetPlatformUser = selectedUsers.SingleOrDefault(platformUser => platformUser.Id == request.TargetPlatformUserId.Value);
            if (targetPlatformUser is null)
            {
                throw new InvalidOperationException("Choose one of the selected viewer rows as the merge target.");
            }
        }

        int[] existingIdentityIds = selectedUsers
            .Where(static platformUser => platformUser.UserIdentityId.HasValue)
            .Select(static platformUser => platformUser.UserIdentityId!.Value)
            .Distinct()
            .OrderBy(static identityId => identityId)
            .ToArray();

        int? targetIdentityId = targetPlatformUser?.UserIdentityId;
        UserIdentity targetIdentity;
        if (targetIdentityId.HasValue)
        {
            targetIdentity = await dbContext.UserIdentities
                .SingleAsync(identity => identity.Id == targetIdentityId.Value, cancellationToken);
        }
        else if (existingIdentityIds.Length > 0 && targetPlatformUser is null)
        {
            targetIdentity = await dbContext.UserIdentities
                .SingleAsync(identity => identity.Id == existingIdentityIds[0], cancellationToken);
        }
        else
        {
            targetIdentity = new UserIdentity();
            dbContext.UserIdentities.Add(targetIdentity);
        }

        string resolvedDisplayName = ResolveCanonicalDisplayName(
            request.CanonicalName,
            targetIdentity.DisplayName,
            targetPlatformUser,
            selectedUsers);
        targetIdentity.DisplayName = resolvedDisplayName;

        int[] sourceIdentityIdsToMove = selectedUsers
            .Where(static platformUser => platformUser.UserIdentityId.HasValue)
            .Select(static platformUser => platformUser.UserIdentityId!.Value)
            .Distinct()
            .Where(identityId => identityId != targetIdentity.Id)
            .OrderBy(static identityId => identityId)
            .ToArray();

        if (sourceIdentityIdsToMove.Length > 0)
        {
            List<PlatformUser> usersFromAdditionalIdentities = await dbContext.PlatformUsers
                .Where(platformUser => platformUser.UserIdentityId.HasValue && sourceIdentityIdsToMove.Contains(platformUser.UserIdentityId.Value))
                .ToListAsync(cancellationToken);

            foreach (PlatformUser platformUser in usersFromAdditionalIdentities)
            {
                platformUser.UserIdentity = targetIdentity;
            }
        }

        foreach (PlatformUser platformUser in selectedUsers)
        {
            platformUser.UserIdentity = targetIdentity;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        long[] mergedUserIds = await dbContext.PlatformUsers
            .AsNoTracking()
            .Where(platformUser => platformUser.UserIdentityId == targetIdentity.Id)
            .OrderBy(platformUser => platformUser.Id)
            .Select(platformUser => platformUser.Id)
            .ToArrayAsync(cancellationToken);

        List<UserIdentitySuggestion> acceptedSuggestions = await dbContext.UserIdentitySuggestions
            .Where(
                suggestion => suggestion.Status == UserIdentitySuggestionStatus.Pending &&
                              mergedUserIds.Contains(suggestion.FirstPlatformUserId) &&
                              mergedUserIds.Contains(suggestion.SecondPlatformUserId))
            .ToListAsync(cancellationToken);

        foreach (UserIdentitySuggestion suggestion in acceptedSuggestions)
        {
            suggestion.Status = UserIdentitySuggestionStatus.Accepted;
        }

        if (sourceIdentityIdsToMove.Length > 0)
        {
            List<UserIdentity> emptyIdentities = await dbContext.UserIdentities
                .Where(identity => sourceIdentityIdsToMove.Contains(identity.Id) && !identity.PlatformUsers.Any())
                .ToListAsync(cancellationToken);

            if (emptyIdentities.Count > 0)
            {
                dbContext.UserIdentities.RemoveRange(emptyIdentities);
            }
        }

        if (acceptedSuggestions.Count > 0 || sourceIdentityIdsToMove.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new UserIdentityMergeResult(targetIdentity.Id, targetIdentity.DisplayName, mergedUserIds);
    }

    public async Task Unlink(long platformUserId, CancellationToken cancellationToken = default)
    {
        if (platformUserId <= 0)
        {
            return;
        }

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool useTransaction = dbContext.Database.IsRelational();
        await using IDbContextTransaction? transaction = useTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        PlatformUser? platformUser = await dbContext.PlatformUsers
            .SingleOrDefaultAsync(user => user.Id == platformUserId, cancellationToken);

        if (platformUser is null || !platformUser.UserIdentityId.HasValue)
        {
            return;
        }

        int userIdentityId = platformUser.UserIdentityId.Value;
        platformUser.UserIdentityId = null;
        platformUser.UserIdentity = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        UserIdentity? emptyIdentity = await dbContext.UserIdentities
            .SingleOrDefaultAsync(identity => identity.Id == userIdentityId && !identity.PlatformUsers.Any(), cancellationToken);

        if (emptyIdentity is not null)
        {
            dbContext.UserIdentities.Remove(emptyIdentity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static string ResolveCanonicalDisplayName(
        string? canonicalName,
        string existingDisplayName,
        PlatformUser? targetPlatformUser,
        IReadOnlyList<PlatformUser> selectedUsers)
    {
        string? normalizedCanonicalName = string.IsNullOrWhiteSpace(canonicalName)
            ? null
            : canonicalName.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedCanonicalName))
        {
            return normalizedCanonicalName;
        }

        if (!string.IsNullOrWhiteSpace(existingDisplayName))
        {
            return existingDisplayName.Trim();
        }

        if (targetPlatformUser is not null && !string.IsNullOrWhiteSpace(targetPlatformUser.DisplayName))
        {
            return targetPlatformUser.DisplayName.Trim();
        }

        return selectedUsers[0].DisplayName.Trim();
    }
}
