using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Instagram;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Remote.Instagram;

public static class InstagramRegistrationExtensions
{
    public static IServiceCollection AddInstagramIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var instagramSection = configuration.GetSection(InstagramOptions.SectionName);

        services.AddOptions<InstagramOptions>()
            .Bind(instagramSection);

        services.AddSingleton<InstagramService>();
        services.AddSingleton<IPlatformConnection>(sp => sp.GetRequiredService<InstagramService>());
        services.AddSingleton<IChatSource>(sp => sp.GetRequiredService<InstagramService>());
        services.AddSingleton<IStreamTarget>(sp => sp.GetRequiredService<InstagramService>());
        services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<InstagramService>());
        services.AddSingleton<IPlatformEventSource>(sp => sp.GetRequiredService<InstagramService>());
        services.AddSingleton<IRtmpRelayDestinationProvider>(sp => sp.GetRequiredService<InstagramService>());

        services.AddSingleton<InstagramConnectionMonitor>();
        services.AddSingleton<IInstagramConnectionMonitor>(sp => sp.GetRequiredService<InstagramConnectionMonitor>());
        services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<InstagramConnectionMonitor>());

        return services;
    }
}
