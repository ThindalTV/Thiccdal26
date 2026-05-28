namespace Thiccdal.Infrastructure.Remotes.Models;

/// <summary>
/// Describes a manual identity merge request and, optionally, which selected row anchors the merged identity.
/// </summary>
public sealed record UserIdentityMergeRequest(
    IReadOnlyList<long> PlatformUserIds,
    long? TargetPlatformUserId,
    string? CanonicalName);
