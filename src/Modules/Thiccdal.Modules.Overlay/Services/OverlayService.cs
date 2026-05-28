using Thiccdal.Infrastructure.Overlay;

namespace Thiccdal.Modules.Overlay.Services;

internal sealed class OverlayService : IOverlayService
{
    private readonly List<IOverlayComponent> _components;
    private readonly Lock _lock = new();

    public OverlayService(IEnumerable<IOverlayComponent>? components = null)
    {
        _components = components?.ToList() ?? [];
    }

    public void Register(IOverlayComponent component)
    {
        lock (_lock)
        {
            _components.Add(component);
        }
    }

    public void Unregister(IOverlayComponent component)
    {
        lock (_lock)
        {
            _components.Remove(component);
        }
    }

    public IReadOnlyList<IOverlayComponent> GetComponents()
    {
        lock (_lock)
        {
            return [.. _components];
        }
    }
}
