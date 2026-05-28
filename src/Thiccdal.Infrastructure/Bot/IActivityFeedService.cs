using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Provides a shared stream activity feed for downstream UI surfaces.
/// </summary>
public interface IActivityFeedService
{
    /// <summary>
    /// Raised whenever a new activity entry is added to the feed.
    /// </summary>
    event EventHandler<ActivityFeedEntry>? EntryAdded;

    /// <summary>
    /// Returns the current feed entries in newest-first order.
    /// </summary>
    /// <returns>The cached activity entries.</returns>
    IReadOnlyList<ActivityFeedEntry> GetEntries();
}
