using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Discord;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Remote.Discord;

public static class DiscordRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDiscordIntegration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var discordSection = configuration.GetSection(DiscordOptions.SectionName);

            services.AddOptions<DiscordOptions>()
                .Bind(discordSection)
                .Validate(
                    static options => options.ReconnectDelaySeconds > 0,
                    "Discord:ReconnectDelaySeconds must be greater than zero.");

            services.AddSingleton<DiscordService>();
            services.AddSingleton<IPlatformConnection>(sp => sp.GetRequiredService<DiscordService>());
            services.AddSingleton<IChatSource>(sp => sp.GetRequiredService<DiscordService>());
            services.AddSingleton<IStreamTarget>(sp => sp.GetRequiredService<DiscordService>());
            services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<DiscordService>());
            services.AddSingleton<IPlatformEventSource>(sp => sp.GetRequiredService<DiscordService>());
            services.AddSingleton<IDiscordService>(sp => sp.GetRequiredService<DiscordService>());

            services.AddSingleton<DiscordConnectionMonitor>();
            services.AddSingleton<IDiscordConnectionMonitor>(sp => sp.GetRequiredService<DiscordConnectionMonitor>());
            services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<DiscordConnectionMonitor>());

            return services;
        }
    }
}
