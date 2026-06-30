using Thiccdal.Infrastructure.Integrations;

namespace Thiccdal.Infrastructure.Instagram;

/// <summary>
/// Monitors Instagram Live connection state. Instagram Live requires explicit API approval from Meta.
/// </summary>
public interface IInstagramConnectionMonitor : IIntegrationConnectionMonitor
{
}
