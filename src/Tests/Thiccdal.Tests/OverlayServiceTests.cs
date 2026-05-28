using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Modules.Overlay.Services;

namespace Thiccdal.Tests;

public sealed class OverlayServiceTests
{
    [Fact]
    public void WhenComponentRegistered_ThenGetComponentsReturnsIt()
    {
        // Arrange
        IOverlayService service = CreateService();
        var component = new TestOverlayComponent("Test Component");

        // Act
        service.Register(component);
        var components = service.GetComponents();

        // Assert
        Assert.Single(components);
        Assert.Contains(component, components);
    }

    [Fact]
    public void WhenComponentUnregistered_ThenGetComponentsDoesNotReturnIt()
    {
        // Arrange
        IOverlayService service = CreateService();
        var component = new TestOverlayComponent("Test Component");
        service.Register(component);

        // Act
        service.Unregister(component);
        var components = service.GetComponents();

        // Assert
        Assert.Empty(components);
    }

    [Fact]
    public void WhenMultipleComponentsRegistered_ThenGetComponentsReturnsAll()
    {
        // Arrange
        IOverlayService service = CreateService();
        var component1 = new TestOverlayComponent("Component 1");
        var component2 = new TestOverlayComponent("Component 2");
        var component3 = new TestOverlayComponent("Component 3");

        // Act
        service.Register(component1);
        service.Register(component2);
        service.Register(component3);
        var components = service.GetComponents();

        // Assert
        Assert.Equal(3, components.Count);
        Assert.Contains(component1, components);
        Assert.Contains(component2, components);
        Assert.Contains(component3, components);
    }

    [Fact]
    public void WhenConcurrentRegistrations_ThenAllComponentsAreRegistered()
    {
        // Arrange
        IOverlayService service = CreateService();
        var components = Enumerable.Range(0, 100)
            .Select(i => new TestOverlayComponent($"Component {i}"))
            .ToList();

        // Act
        Parallel.ForEach(components, component => service.Register(component));
        var registeredComponents = service.GetComponents();

        // Assert
        Assert.Equal(100, registeredComponents.Count);
        foreach (var component in components)
        {
            Assert.Contains(component, registeredComponents);
        }
    }

    [Fact]
    public void WhenConstructedWithSeedComponents_ThenGetComponentsReturnsSeededComponents()
    {
        // Arrange
        TestOverlayComponent[] seededComponents =
        [
            new TestOverlayComponent("Chat Feed"),
            new TestOverlayComponent("Event Ticker"),
            new TestOverlayComponent("Lower Third")
        ];

        IOverlayService service = new OverlayService(seededComponents);

        // Act
        IReadOnlyList<IOverlayComponent> components = service.GetComponents();

        // Assert
        Assert.Equal(3, components.Count);
        Assert.Equal(seededComponents.Select(component => component.ComponentName), components.Select(component => component.ComponentName));
    }

    private static IOverlayService CreateService()
    {
        return new OverlayService();
    }

    private sealed class TestOverlayComponent : IOverlayComponent
    {
        public TestOverlayComponent(string name)
        {
            ComponentName = name;
        }

        public string ComponentName { get; }

        public Type ComponentType => GetType();
    }
}
