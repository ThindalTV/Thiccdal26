using Thiccdal.Infrastructure.Integrations;

namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Monitors YouTube OAuth token validity and notifies observers when connection state changes.
/// </summary>
public interface IYouTubeConnectionMonitor : IIntegrationConnectionMonitor
{
}
