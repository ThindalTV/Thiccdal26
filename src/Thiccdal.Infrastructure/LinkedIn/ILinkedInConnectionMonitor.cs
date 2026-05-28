using Thiccdal.Infrastructure.Integrations;

namespace Thiccdal.Infrastructure.LinkedIn;

/// <summary>
/// Monitors LinkedIn connection state. LinkedIn Live requires explicit API approval from LinkedIn.
/// </summary>
public interface ILinkedInConnectionMonitor : IIntegrationConnectionMonitor
{
}
