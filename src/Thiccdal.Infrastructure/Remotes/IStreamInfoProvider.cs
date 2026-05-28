namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Exposes platform-specific stream metadata updates through a platform-agnostic seam.
/// </summary>
public interface IStreamInfoProvider
{
    /// <summary>
    /// Gets the platform display name used by operator surfaces.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Attempts to push pre-live stream metadata to the underlying platform.
    /// </summary>
    /// <param name="request">The requested stream metadata update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The per-platform update result.</returns>
    Task<StreamInfoUpdateResult> UpdateStreamInfo(
        StreamInfoUpdateRequest request,
        CancellationToken cancellationToken = default);
}
