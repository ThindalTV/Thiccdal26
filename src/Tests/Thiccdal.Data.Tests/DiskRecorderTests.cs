using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.RtmpServer.Services;

namespace Thiccdal.Data.Tests;

public sealed class DiskRecorderTests : IAsyncDisposable
{
    private readonly List<string> _pathsToDelete = [];

    [Fact]
    public async Task WhenRecordingStarts_ThenRecordingRowTracksSessionAndFilePath()
    {
        Guid sessionId = Guid.NewGuid();
        FakeRecordingProcess process = new();
        FakeRecordingProcessRunner processRunner = new(process);
        ServiceProvider provider = BuildProvider(
            processRunner,
            CreatePath("disk-recorder-output"));

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IDiskRecorder diskRecorder = scope.ServiceProvider.GetRequiredService<IDiskRecorder>();
        IStreamRecordingService streamRecordingService = scope.ServiceProvider.GetRequiredService<IStreamRecordingService>();

        await diskRecorder.Start(sessionId: sessionId);

        StreamRecordingSnapshot started = Assert.IsType<StreamRecordingSnapshot>(await streamRecordingService.GetLatest("Local"));
        RecordingProcessRequest request = Assert.IsType<RecordingProcessRequest>(processRunner.LastRequest);

        Assert.Equal(sessionId, started.SessionId);
        Assert.Equal(request.OutputPath, started.FilePath);
        Assert.Contains(sessionId.ToString("N"), started.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Null(started.EndedAt);
        Assert.True(diskRecorder.IsRecording);
    }

    [Fact]
    public async Task WhenRecordingStops_ThenRecordingRowSetsEndedAtAndKeepsFilePath()
    {
        FakeRecordingProcess process = new();
        FakeRecordingProcessRunner processRunner = new(process);
        ServiceProvider provider = BuildProvider(
            processRunner,
            CreatePath("disk-recorder-stop-output"));

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IDiskRecorder diskRecorder = scope.ServiceProvider.GetRequiredService<IDiskRecorder>();
        IStreamRecordingService streamRecordingService = scope.ServiceProvider.GetRequiredService<IStreamRecordingService>();

        await diskRecorder.Start();
        StreamRecordingSnapshot started = Assert.IsType<StreamRecordingSnapshot>(await streamRecordingService.GetLatest("Local"));

        await diskRecorder.Stop();

        StreamRecordingSnapshot stopped = Assert.IsType<StreamRecordingSnapshot>(await streamRecordingService.GetLatest("Local"));
        Assert.Equal(started.FilePath, stopped.FilePath);
        Assert.NotNull(stopped.EndedAt);
        Assert.Equal(string.Empty, stopped.Error);
        Assert.False(diskRecorder.IsRecording);
    }

    [Fact]
    public async Task WhenProcessLaunchFails_ThenRecordingRowCapturesErrorAndPreservesFilePath()
    {
        ThrowingRecordingProcessRunner processRunner = new("ffmpeg missing");
        ServiceProvider provider = BuildProvider(
            processRunner,
            CreatePath("disk-recorder-errors"));

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IDiskRecorder diskRecorder = scope.ServiceProvider.GetRequiredService<IDiskRecorder>();
        IStreamRecordingService streamRecordingService = scope.ServiceProvider.GetRequiredService<IStreamRecordingService>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => diskRecorder.Start());

        StreamRecordingSnapshot failed = Assert.IsType<StreamRecordingSnapshot>(await streamRecordingService.GetLatest("Local"));
        RecordingProcessRequest request = Assert.IsType<RecordingProcessRequest>(processRunner.LastRequest);

        Assert.Contains("ffmpeg missing", exception.Message, StringComparison.Ordinal);
        Assert.Equal(request.OutputPath, failed.FilePath);
        Assert.NotNull(failed.EndedAt);
        Assert.Contains("ffmpeg missing", failed.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenRecordingProcessExitsUnexpectedly_ThenRecordingRowCapturesExitCodeAndPreservesFilePath()
    {
        FakeRecordingProcess process = new();
        FakeRecordingProcessRunner processRunner = new(process);
        ServiceProvider provider = BuildProvider(
            processRunner,
            CreatePath("disk-recorder-exit-output"));

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IDiskRecorder diskRecorder = scope.ServiceProvider.GetRequiredService<IDiskRecorder>();
        IStreamRecordingService streamRecordingService = scope.ServiceProvider.GetRequiredService<IStreamRecordingService>();

        await diskRecorder.Start();
        StreamRecordingSnapshot started = Assert.IsType<StreamRecordingSnapshot>(await streamRecordingService.GetLatest("Local"));

        process.TriggerExit(2);
        await WaitFor(async () =>
        {
            StreamRecordingSnapshot? current = await streamRecordingService.GetLatest("Local");
            return current?.EndedAt is not null;
        });

        StreamRecordingSnapshot failed = Assert.IsType<StreamRecordingSnapshot>(await streamRecordingService.GetLatest("Local"));
        Assert.Equal(started.FilePath, failed.FilePath);
        Assert.Contains("code 2", failed.Error, StringComparison.Ordinal);
        Assert.False(diskRecorder.IsRecording);
    }

    public ValueTask DisposeAsync()
    {
        foreach (string path in _pathsToDelete)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        return ValueTask.CompletedTask;
    }

    private ServiceProvider BuildProvider(IRecordingProcessRunner processRunner, string recordingOutputPath)
    {
        string databasePath = CreatePath($"{Guid.NewGuid():N}.db");

        ConfigurationManager configuration = new();
        configuration[$"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.DefaultConnection)}"] =
            $"Data Source={databasePath}";
        configuration[$"{StreamingOptions.SectionName}:{nameof(StreamingOptions.IngestUrl)}"] =
            "rtmp://localhost:1935/live/tests";
        configuration[$"{StreamingOptions.SectionName}:{nameof(StreamingOptions.RecordingOutputPath)}"] =
            recordingOutputPath;
        configuration[$"{StreamingOptions.SectionName}:{nameof(StreamingOptions.FfmpegExecutablePath)}"] =
            "ffmpeg";

        ServiceCollection services = new();
        services.AddLogging();
        services.AddOptions<StreamingOptions>()
            .Bind(configuration.GetSection(StreamingOptions.SectionName));
        services.AddThiccdalData(configuration);
        services.AddSingleton<IRtmpIngestListener, FakeRtmpIngestListener>();
        services.AddSingleton<IRecordingProcessRunner>(processRunner);
        services.AddSingleton<IDiskRecorder, DiskRecorder>();
        services.AddSingleton<IStreamingService, StreamingService>();

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<ConnectionStringsOptions>>();
        provider.InitializeDatabase().GetAwaiter().GetResult();
        return provider;
    }

    private string CreatePath(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        _pathsToDelete.Add(path);
        return path;
    }

    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(await condition());
    }

    private sealed class FakeRecordingProcessRunner : IRecordingProcessRunner
    {
        private readonly FakeRecordingProcess _process;

        public FakeRecordingProcessRunner(FakeRecordingProcess process)
        {
            _process = process;
        }

        public RecordingProcessRequest? LastRequest { get; private set; }

        public IRecordingProcess Start(RecordingProcessRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return _process;
        }
    }

    private sealed class ThrowingRecordingProcessRunner : IRecordingProcessRunner
    {
        private readonly string _message;

        public ThrowingRecordingProcessRunner(string message)
        {
            _message = message;
        }

        public RecordingProcessRequest? LastRequest { get; private set; }

        public IRecordingProcess Start(RecordingProcessRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            throw new InvalidOperationException(_message);
        }
    }

    private sealed class FakeRecordingProcess : IRecordingProcess
    {
        public event EventHandler? Exited;

        public bool HasExited { get; private set; }

        public int ExitCode { get; private set; }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TriggerExit(0);
            return Task.CompletedTask;
        }

        public void TriggerExit(int exitCode)
        {
            HasExited = true;
            ExitCode = exitCode;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeRtmpIngestListener : IRtmpIngestListener
    {
        public bool IsListening => false;

        public event EventHandler<RtmpIngestStateChanged>? StateChanged
        {
            add { }
            remove { }
        }

        public Task Start(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
