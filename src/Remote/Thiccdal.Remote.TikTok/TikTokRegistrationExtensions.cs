using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.TikTok;

namespace Thiccdal.Remote.TikTok;

public static class TikTokRegistrationExtensions
{
    public static IServiceCollection AddTikTokIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var tikTokSection = configuration.GetSection(TikTokOptions.SectionName);

        services.AddOptions<TikTokOptions>()
            .Bind(tikTokSection);

        services.AddSingleton<TikTokService>();
        services.AddSingleton<IPlatformConnection>(sp => sp.GetRequiredService<TikTokService>());
        services.AddSingleton<IChatSource>(sp => sp.GetRequiredService<TikTokService>());
        services.AddSingleton<IStreamTarget>(sp => sp.GetRequiredService<TikTokService>());
        services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<TikTokService>());
        services.AddSingleton<IPlatformEventSource>(sp => sp.GetRequiredService<TikTokService>());
        services.AddSingleton<IRtmpRelayDestinationProvider>(sp => sp.GetRequiredService<TikTokService>());

        services.AddSingleton<TikTokConnectionMonitor>();
        services.AddSingleton<ITikTokConnectionMonitor>(sp => sp.GetRequiredService<TikTokConnectionMonitor>());
        services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<TikTokConnectionMonitor>());

        return services;
    }
}
