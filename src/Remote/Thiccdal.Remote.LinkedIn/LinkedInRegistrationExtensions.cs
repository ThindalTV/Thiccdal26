using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.LinkedIn;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Remote.LinkedIn;

public static class LinkedInRegistrationExtensions
{
    public static IServiceCollection AddLinkedInIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var linkedInSection = configuration.GetSection(LinkedInOptions.SectionName);

        services.AddOptions<LinkedInOptions>()
            .Bind(linkedInSection);

        services.AddSingleton<LinkedInService>();
        services.AddSingleton<IPlatformConnection>(sp => sp.GetRequiredService<LinkedInService>());
        services.AddSingleton<IChatSource>(sp => sp.GetRequiredService<LinkedInService>());
        services.AddSingleton<IStreamTarget>(sp => sp.GetRequiredService<LinkedInService>());
        services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<LinkedInService>());
        services.AddSingleton<IPlatformEventSource>(sp => sp.GetRequiredService<LinkedInService>());
        services.AddSingleton<IRtmpRelayDestinationProvider>(sp => sp.GetRequiredService<LinkedInService>());

        services.AddSingleton<LinkedInConnectionMonitor>();
        services.AddSingleton<ILinkedInConnectionMonitor>(sp => sp.GetRequiredService<LinkedInConnectionMonitor>());
        services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<LinkedInConnectionMonitor>());

        return services;
    }
}
