using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Upserts normalized platform users identified by platform and platform user id.
/// </summary>
public interface IPlatformUserService
{
    /// <summary>
    /// Creates or updates the user record for the supplied platform identity.
    /// </summary>
    /// <param name="source">The platform that owns the user identifier.</param>
    /// <param name="platformUserId">The platform-specific user identifier.</param>
    /// <param name="displayName">The latest display name reported for the user.</param>
    /// <param name="lastSeen">The latest time the user was observed.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The persisted platform user id.</returns>
    Task<long> Upsert(
        PlatformEventSource source,
        string platformUserId,
        string displayName,
        DateTime lastSeen,
        CancellationToken cancellationToken = default);
}
