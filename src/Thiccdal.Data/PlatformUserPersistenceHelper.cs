using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;
using RuntimePlatformEventSource = Thiccdal.Infrastructure.Bot.Models.PlatformEventSource;

namespace Thiccdal.Data;

internal static class PlatformUserPersistenceHelper
{
    public static async Task<PlatformUser> Upsert(
        ApplicationDbContext dbContext,
        RuntimePlatformEventSource source,
        string platformUserId,
        string displayName,
        DateTime lastSeen,
        CancellationToken cancellationToken)
    {
        PlatformUser? platformUser = await dbContext.PlatformUsers.SingleOrDefaultAsync(
            user => user.Source == source && user.PlatformUserId == platformUserId,
            cancellationToken);

        if (platformUser is null)
        {
            platformUser = new PlatformUser
            {
                Source = source,
                PlatformUserId = platformUserId,
                DisplayName = displayName,
                LastSeen = lastSeen
            };

            dbContext.PlatformUsers.Add(platformUser);
            return platformUser;
        }

        if (!string.Equals(platformUser.DisplayName, displayName, StringComparison.Ordinal))
        {
            platformUser.DisplayName = displayName;
        }

        if (platformUser.LastSeen < lastSeen)
        {
            platformUser.LastSeen = lastSeen;
        }

        return platformUser;
    }
}
