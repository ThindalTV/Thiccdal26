using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

/// <summary>
/// Registers the remote RTMP client adapter services with the dependency injection container.
/// </summary>
public static class StreamingRegistrationExtensions
{
    /// <summary>
    /// Adds the remote streaming client services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddStreamingServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<IRtmpServerClient, RtmpServerClient>();
        services.AddSingleton<RemoteStreamingService>();
        services.AddSingleton<IStreamingService>(static sp => sp.GetRequiredService<RemoteStreamingService>());
        services.AddHostedService(static sp => sp.GetRequiredService<RemoteStreamingService>());
        services.AddSingleton<IRtmpFanoutService, RemoteRtmpFanoutService>();

        return services;
    }
}
