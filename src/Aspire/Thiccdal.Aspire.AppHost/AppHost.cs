var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Thiccdal>("thiccdal")
    .WithEnvironment("Streaming__IngestUrl", "rtmp://localhost:1935/live")
    .WithEnvironment("Streaming__FfmpegExecutablePath", "ffmpeg");

builder.Build().Run();
