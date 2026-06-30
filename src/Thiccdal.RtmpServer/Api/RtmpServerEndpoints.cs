using Thiccdal.Infrastructure.Streaming;
using Thiccdal.RtmpServer.Services;

namespace Thiccdal.RtmpServer.Api;

/// <summary>
/// Extension methods that register the RTMP server's minimal API endpoints.
/// </summary>
public static class RtmpServerEndpoints
{
    /// <summary>
    /// Maps the RTMP server's HTTP API endpoints onto the application.
    /// </summary>
    public static IEndpointRouteBuilder MapRtmpServerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/status", GetStatus);
        app.MapPost("/api/config", PostConfig);
        app.MapPost("/api/start", PostStart);
        app.MapPost("/api/stop", PostStop);
        return app;
    }

    private static IResult GetStatus(
        IStreamingService streaming,
        RtmpFanoutService fanout,
        IDiskRecorder recorder)
    {
        RtmpServerStatusResponse response = new RtmpServerStatusResponse(
            IsIngestRunning: streaming.IsRunning,
            IsFanoutRunning: fanout.IsRunning,
            IsRecording: recorder.IsRecording,
            IngestState: streaming.State.ToString(),
            ActiveRelayCount: fanout.ActiveRelayCount,
            ErrorMessage: string.Empty);

        return Results.Ok(response);
    }

    private static IResult PostConfig(
        RtmpServerConfigurationPush config,
        IRtmpServerConfigurationHolder holder)
    {
        holder.Apply(config);
        return Results.Ok();
    }

    private static async Task<IResult> PostStart(
        IStreamingService streaming,
        IRtmpFanoutService fanout,
        CancellationToken cancellationToken)
    {
        await streaming.Start(cancellationToken);
        await fanout.StartFanout(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> PostStop(
        IStreamingService streaming,
        IRtmpFanoutService fanout,
        CancellationToken cancellationToken)
    {
        await fanout.StopFanout(cancellationToken);
        await streaming.Stop(cancellationToken);
        return Results.Ok();
    }
}
