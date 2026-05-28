using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

public static class StreamingRegistrationExtensions
{
    public static IServiceCollection AddStreamingServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRtmpIngestListener, RtmpIngestListener>();
        services.AddSingleton<IStreamingRelaySessionFactory, FfmpegStreamingRelaySessionFactory>();
        services.AddSingleton<IBrbSlateInjector, BrbSlateInjector>();
        services.AddSingleton<IRecordingProcessRunner, FfmpegRecordingProcessRunner>();
        services.AddSingleton<IDiskRecorder, DiskRecorder>();
        services.AddSingleton<IStreamingService, StreamingService>();
        services.AddSingleton<IRtmpFanoutService, RtmpFanoutService>();
        services.AddHostedService<RestreamBootstrapService>();

        return services;
    }
}
