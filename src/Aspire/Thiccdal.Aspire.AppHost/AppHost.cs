var builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> rtmpServer = builder
    .AddProject<Projects.Thiccdal_RtmpServer>("rtmp-server")
    .WithEnvironment("RtmpServer__ApiKey", "dev-api-key");

builder.AddProject<Projects.Thiccdal>("thiccdal")
    .WithEnvironment("Streaming__IngestUrl", "rtmp://localhost:1935/live")
    .WithEnvironment("Streaming__FfmpegExecutablePath", "ffmpeg")
    .WithEnvironment("RtmpServer__ApiKey", "dev-api-key")
    .WithEnvironment("RtmpServer__BaseUrl", rtmpServer.GetEndpoint("http"));

builder.Build().Run();
