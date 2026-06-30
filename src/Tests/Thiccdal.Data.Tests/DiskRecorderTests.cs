using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.RtmpServer.Services;

namespace Thiccdal.Data.Tests;

public sealed class DiskRecorderTests : IAsyncDisposable
{
    private readonly List<string> _pathsToDelete = [];

    [Fact]
    public async Task WhenRecordingStarts_ThenIsRecordingAndRequestContainsSessionAndPath()
    {
        Guid sessionId = Guid.NewGuid();
        FakeRecordingProcess process = new();
        FakeRecordingProcessRunner processRunner = new(process);
        IDiskRecorder diskRecorder = BuildDiskRecorder(processRunner, CreatePath("disk-recorder-output"));

        await diskRecorder.Start(sessionId: sessionId);

        RecordingProcessRequest request = Assert.IsType<RecordingProcessRequest>(processRunner.LastRequest);
        Assert.True(diskRecorder.IsRecording);
        Assert.Contains(sessionId.ToString("N"), request.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("rtmp://localhost:1935/live/tests", request.IngestUrl);
        Assert.Equal("ffmpeg", request.ExecutablePath);
    }

    [Fact]
    public async Task WhenRecordingStops_ThenIsRecordingBecomesFalse()
    {
        FakeRecordingProcess process = new();
        FakeRecordingProcessRunner processRunner = new(process);
        IDiskRecorder diskRecorder = BuildDiskRecorder(processRunner, CreatePath("disk-recorder-stop-output"));

        await diskRecorder.Start();
        Assert.True(diskRecorder.IsRecording);

        await diskRecorder.Stop();

        Assert.False(diskRecorder.IsRecording);
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task WhenProcessLaunchFails_ThenInvalidOperationExceptionIsThrown()
    {
        ThrowingRecordingProcessRunner processRunner = new("ffmpeg missing");
        IDiskRecorder diskRecorder = BuildDiskRecorder(processRunner, CreatePath("disk-recorder-errors"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => diskRecorder.Start());

        Assert.Contains("ffmpeg missing", exception.Message, StringComparison.Ordinal);
        Assert.False(diskRecorder.IsRecording);
    }

    [Fact]
    public async Task WhenRecordingProcessExitsUnexpectedly_ThenIsRecordingBecomesFalse()
    {
        FakeRecordingProcess process = new();
        FakeRecordingProcessRunner processRunner = new(process);
        IDiskRecorder diskRecorder = BuildDiskRecorder(processRunner, CreatePath("disk-recorder-exit-output"));

        await diskRecorder.Start();
        Assert.True(diskRecorder.IsRecording);

        process.TriggerExit(2);
        await WaitFor(() => Task.FromResult(!diskRecorder.IsRecording));

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

    private IDiskRecorder BuildDiskRecorder(IRecordingProcessRunner processRunner, string recordingOutputPath)
    {
        RtmpServerConfigurationHolder configHolder = new RtmpServerConfigurationHolder();
        configHolder.Apply(new RtmpServerConfigurationPush(
            IngestUrl: "rtmp://localhost:1935/live/tests",
            RecordingOutputPath: recordingOutputPath,
            BrbSlatePath: string.Empty,
            Destinations: Array.Empty<RtmpRelayDestinationPush>()));

        ServiceCollection services = new();
        services.AddLogging();
        services.Configure<RtmpServerOptions>(opts => opts.FfmpegExecutablePath = "ffmpeg");
        services.AddSingleton<IRtmpServerConfigurationHolder>(configHolder);
        services.AddSingleton<IRecordingProcessRunner>(processRunner);
        services.AddSingleton<IDiskRecorder, DiskRecorder>();

        return services.BuildServiceProvider().GetRequiredService<IDiskRecorder>();
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
}
