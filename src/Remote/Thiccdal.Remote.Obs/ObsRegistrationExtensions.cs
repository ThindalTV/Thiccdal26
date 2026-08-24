using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Remote.Obs;

/// <summary>
/// Registers the OBS Studio integration.
/// </summary>
public static class ObsRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the obs-websocket connection and the hosted service that keeps it open.
        /// </summary>
        /// <param name="configuration">The application configuration root.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddObsIntegration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<ObsOptions>()
                .Bind(configuration.GetSection(ObsOptions.SectionName))
                .Validate(
                    static options => !string.IsNullOrWhiteSpace(options.Host),
                    "Obs:Host must not be empty.")
                .Validate(
                    static options => options.Port is > 0 and <= 65535,
                    "Obs:Port must be a valid TCP port.")
                .Validate(
                    static options => options.InitialReconnectDelaySeconds > 0,
                    "Obs:InitialReconnectDelaySeconds must be greater than zero.")
                .Validate(
                    static options => options.MaxReconnectDelaySeconds >= options.InitialReconnectDelaySeconds,
                    "Obs:MaxReconnectDelaySeconds must be at least Obs:InitialReconnectDelaySeconds.");

            services.AddSingleton<ObsWebSocketConnection>();
            services.AddSingleton<IObsConnection>(static serviceProvider => serviceProvider.GetRequiredService<ObsWebSocketConnection>());
            services.AddHostedService<ObsConnectionHostedService>();

            return services;
        }
    }
}
