using Thiccdal.Infrastructure.Integrations;

namespace Thiccdal.Infrastructure.TikTok;

/// <summary>
/// Monitors TikTok Live connection state. TikTok Live requires explicit API approval from TikTok.
/// </summary>
public interface ITikTokConnectionMonitor : IIntegrationConnectionMonitor
{
}
