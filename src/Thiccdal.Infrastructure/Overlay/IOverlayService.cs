namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// Registry for overlay components. Provides enumeration for the overlay surface and operator UI.
/// </summary>
public interface IOverlayService
{
    /// <summary>
    /// Registers an overlay component with the service.
    /// </summary>
    /// <param name="component">The component to register.</param>
    void Register(IOverlayComponent component);

    /// <summary>
    /// Unregisters an overlay component from the service.
    /// </summary>
    /// <param name="component">The component to unregister.</param>
    void Unregister(IOverlayComponent component);

    /// <summary>
    /// Gets all currently registered overlay components.
    /// </summary>
    /// <returns>A read-only list of registered components.</returns>
    IReadOnlyList<IOverlayComponent> GetComponents();
}
