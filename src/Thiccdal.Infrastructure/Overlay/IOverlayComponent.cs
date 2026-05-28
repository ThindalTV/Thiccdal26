namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// Contract for a component that can be registered with the overlay registry and rendered on the /overlay page.
/// </summary>
public interface IOverlayComponent
{
    /// <summary>
    /// Gets the human-readable name shown in operator UI and Pre-Live Checklist.
    /// </summary>
    string ComponentName { get; }

    /// <summary>
    /// Gets the Blazor component Type rendered on the /overlay page.
    /// </summary>
    Type ComponentType { get; }
}
