using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Modules.Overlay.Components;
using Thiccdal.Modules.Overlay.Services;

namespace Thiccdal.Modules.Overlay;

public static class OverlayRegistrationExtension
{
    public static IServiceCollection AddOverlay(this IServiceCollection services)
    {
        services.AddSingleton<IOverlayComponent>(new TestableOverlayComponentRegistration("Chat Feed", typeof(ChatFeedOverlayComponent)));
        services.AddSingleton<IOverlayComponent>(new TestableOverlayComponentRegistration("Event Ticker", typeof(EventTickerOverlayComponent)));
        services.AddSingleton<IOverlayComponent>(new TestableOverlayComponentRegistration("Lower Third", typeof(LowerThirdOverlayComponent)));
        services.AddSingleton<IOverlayService>(
            static serviceProvider => new OverlayService(serviceProvider.GetServices<IOverlayComponent>()));
        services.AddSingleton<IPlatformManualReminderProvider, PlatformManualReminderProvider>();
        return services;
    }

    public static IServiceCollection AddOverlayModule(this IServiceCollection services)
    {
        return services.AddOverlay();
    }

    private sealed class TestableOverlayComponentRegistration : ITestableOverlayComponent
    {
        public TestableOverlayComponentRegistration(string componentName, Type componentType)
        {
            ComponentName = componentName;
            ComponentType = componentType;
        }

        public string ComponentName { get; }

        public Type ComponentType { get; }

        public Task TriggerTestFlash(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
