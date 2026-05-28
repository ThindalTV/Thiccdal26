using Thiccdal.Infrastructure.Integrations;

namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Twitch-specific integration connection monitor.
/// Consumers that care only about Twitch can inject this directly;
/// consumers rendering all platforms inject <see cref="IIntegrationConnectionMonitor"/>.
/// </summary>
public interface ITwitchConnectionMonitor : IIntegrationConnectionMonitor
{
}
