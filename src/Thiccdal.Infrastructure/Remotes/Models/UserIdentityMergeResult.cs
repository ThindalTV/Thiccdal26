namespace Thiccdal.Infrastructure.Remotes.Models;

/// <summary>
/// Describes the canonical identity after a merge operation completes.
/// </summary>
public sealed record UserIdentityMergeResult(
    int UserIdentityId,
    string DisplayName,
    IReadOnlyList<long> PlatformUserIds);
