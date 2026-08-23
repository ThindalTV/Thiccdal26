using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Remote.Null;

public static class NullRegistrationExtensions
{
    public static IServiceCollection AddNullIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<NullOptions>()
            .Bind(configuration.GetSection(NullOptions.SectionName));

        services.AddSingleton<NullPlatformConnection>();
        services.AddSingleton<IPlatformConnection>(static serviceProvider => serviceProvider.GetRequiredService<NullPlatformConnection>());
        services.AddSingleton<IChatSource>(static serviceProvider => serviceProvider.GetRequiredService<NullPlatformConnection>());
        services.AddSingleton<IStreamTarget>(static serviceProvider => serviceProvider.GetRequiredService<NullPlatformConnection>());
        services.AddSingleton<IEventSource>(static serviceProvider => serviceProvider.GetRequiredService<NullPlatformConnection>());
        services.AddSingleton<IPlatformEventSource>(static serviceProvider => serviceProvider.GetRequiredService<NullPlatformConnection>());
        services.AddSingleton<IIntegrationConnectionMonitor>(static serviceProvider => serviceProvider.GetRequiredService<NullPlatformConnection>());
        services.AddSingleton<IPlatformManualReminderProvider, NullPlatformManualReminderProvider>();

        return services;
    }
}
