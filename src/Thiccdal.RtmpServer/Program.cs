using Thiccdal.Infrastructure.Streaming;
using Thiccdal.RtmpServer.Api;
using Thiccdal.RtmpServer.Hubs;
using Thiccdal.RtmpServer.Middleware;
using Thiccdal.RtmpServer.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RtmpServerOptions>(builder.Configuration.GetSection(RtmpServerOptions.SectionName));
builder.Services.Configure<StreamingOptions>(builder.Configuration.GetSection(StreamingOptions.SectionName));
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRtmpServerConfigurationHolder, RtmpServerConfigurationHolder>();
builder.Services.AddSingleton<RtmpEventPublisher>();
builder.Services.AddSingleton<IRtmpIngestListener, RtmpIngestListener>();
builder.Services.AddSingleton<IStreamingRelaySessionFactory, FfmpegStreamingRelaySessionFactory>();
builder.Services.AddSingleton<IBrbSlateInjector, BrbSlateInjector>();
builder.Services.AddSingleton<IRecordingProcessRunner, FfmpegRecordingProcessRunner>();
builder.Services.AddSingleton<IDiskRecorder, DiskRecorder>();
builder.Services.AddSingleton<IStreamingService, StreamingService>();
builder.Services.AddSingleton<RtmpFanoutService>();
builder.Services.AddSingleton<IRtmpFanoutService>(static sp => sp.GetRequiredService<RtmpFanoutService>());
builder.Services.AddHostedService<RtmpEventBridgeService>();

WebApplication app = builder.Build();

app.UseMiddleware<ApiKeyMiddleware>();
app.MapHub<RtmpEventsHub>("/hubs/events");
app.MapRtmpServerEndpoints();
app.MapGet("/healthz", () => Results.Ok());

app.Run();
