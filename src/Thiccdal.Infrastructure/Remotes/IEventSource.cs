using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents a source of normalized platform events.
/// </summary>
public interface IEventSource
{
    /// <summary>
    /// Raised when a normalized platform event is received from the source.
    /// </summary>
    event EventHandler<PlatformEvent>? OnPlatformEventReceived;
}
