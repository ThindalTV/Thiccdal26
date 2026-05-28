using Thiccdal.Infrastructure.Integrations;

namespace Thiccdal.Infrastructure.Discord;

/// <summary>
/// Monitors the Discord connection state for UI binding.
/// </summary>
public interface IDiscordConnectionMonitor : IIntegrationConnectionMonitor
{
    /// <summary>
    /// Gets the current Discord relay capability state.
    /// </summary>
    DiscordRelayStatus RelayStatus { get; }
}
