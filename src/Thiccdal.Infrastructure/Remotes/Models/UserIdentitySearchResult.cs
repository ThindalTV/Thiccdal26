using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes.Models;

/// <summary>
/// Represents one operator-visible platform user in the identity review surface.
/// </summary>
public sealed record UserIdentitySearchResult(
    long PlatformUserId,
    PlatformEventSource Source,
    string PlatformUserKey,
    string DisplayName,
    DateTime LastSeen,
    int? UserIdentityId,
    string? UserIdentityDisplayName);
