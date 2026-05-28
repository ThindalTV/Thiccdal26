using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Represents the infrastructure-first YouTube platform connection seam.
/// </summary>
public interface IYouTubePlatformConnection : IPlatformConnection, IYouTubeBroadcastInfoProvider
{
}
