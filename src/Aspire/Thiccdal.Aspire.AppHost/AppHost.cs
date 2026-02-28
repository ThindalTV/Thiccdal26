var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Thiccdal>("thiccdal");

builder.Build().Run();
