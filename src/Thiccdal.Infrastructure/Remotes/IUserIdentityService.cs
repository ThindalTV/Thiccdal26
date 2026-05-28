using Thiccdal.Infrastructure.Remotes.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Provides operator-facing search and merge actions for cross-platform viewer identities.
/// </summary>
public interface IUserIdentityService
{
    /// <summary>
    /// Searches persisted platform users by viewer or canonical identity name.
    /// </summary>
    /// <param name="query">The operator-entered search query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching platform-user rows.</returns>
    Task<IReadOnlyList<UserIdentitySearchResult>> Search(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges the supplied platform users into one canonical identity using an explicit merge request.
    /// </summary>
    /// <param name="request">The merge request, including selected rows and an optional target row.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The merged identity summary.</returns>
    Task<UserIdentityMergeResult> Merge(UserIdentityMergeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges the supplied platform users into one canonical identity.
    /// </summary>
    /// <param name="platformUserIds">The selected platform-user identifiers.</param>
    /// <param name="canonicalName">The optional operator-supplied canonical display name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The merged identity summary.</returns>
    Task<UserIdentityMergeResult> Merge(
        IReadOnlyList<long> platformUserIds,
        string? canonicalName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a platform user from its current identity, if one exists.
    /// </summary>
    /// <param name="platformUserId">The platform-user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Unlink(long platformUserId, CancellationToken cancellationToken = default);
}
