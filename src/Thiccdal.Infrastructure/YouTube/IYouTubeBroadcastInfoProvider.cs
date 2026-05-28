namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Exposes YouTube broadcast metadata operations available to operator surfaces.
/// </summary>
public interface IYouTubeBroadcastInfoProvider
{
    /// <summary>
    /// Updates the active broadcast title.
    /// </summary>
    Task SetTitle(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the active broadcast description.
    /// </summary>
    Task SetDescription(string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to update the active broadcast category.
    /// </summary>
    Task SetCategory(string category, CancellationToken cancellationToken = default);
}
