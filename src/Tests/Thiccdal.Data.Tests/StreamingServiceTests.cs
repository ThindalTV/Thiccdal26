using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.RtmpServer.Services;

namespace Thiccdal.Data.Tests;

public sealed class StreamingServiceTests
{
    [Fact]
    public async Task WhenStreamingStarts_ThenIngestListenerStartsAndStateWaitsForIngest()
    {
        FakeRtmpIngestListener ingestListener = new();
        FakeDiskRecorder diskRecorder = new();
        StreamingService service = CreateService(ingestListener, diskRecorder);

        await service.Start();

        Assert.True(service.IsRunning);
        Assert.Equal(StreamingState.WaitingForIngest, service.State);
        Assert.True(ingestListener.IsListening);
        Assert.Equal(1, ingestListener.StartCount);
        Assert.Equal(0, diskRecorder.StartCount);
    }

    [Fact]
    public async Task WhenIngestTransitionsLive_ThenRecorderStartsWithSessionAndStateBecomesLive()
    {
        FakeRtmpIngestListener ingestListener = new();
        FakeDiskRecorder diskRecorder = new();
        StreamingService service = CreateService(ingestListener, diskRecorder);
        Guid sessionId = Guid.NewGuid();

        await service.Start(sessionId: sessionId);
        ingestListener.Publish(StreamingState.Live, "OBS ingest connected.");
        await WaitFor(() => diskRecorder.StartCount == 1 && service.State == StreamingState.Live);

        Assert.Equal(sessionId, Assert.Single(diskRecorder.SessionIds));
        Assert.True(diskRecorder.IsRecording);
    }

    [Fact]
    public async Task WhenIngestDisconnects_ThenRecorderStopsAndStateBecomesBrbSlate()
    {
        FakeRtmpIngestListener ingestListener = new();
        FakeDiskRecorder diskRecorder = new();
        StreamingService service = CreateService(ingestListener, diskRecorder);

        await service.Start();
        ingestListener.Publish(StreamingState.Live, "OBS ingest connected.");
        await WaitFor(() => diskRecorder.StartCount == 1);

        ingestListener.Publish(StreamingState.BrbSlate, "OBS ingest disconnected.");
        await WaitFor(() => diskRecorder.StopCount == 1 && service.State == StreamingState.BrbSlate);

        Assert.False(diskRecorder.IsRecording);
    }

    [Fact]
    public async Task WhenIngestReportsError_ThenRecorderStopsAndStateBecomesError()
    {
        FakeRtmpIngestListener ingestListener = new();
        FakeDiskRecorder diskRecorder = new();
        StreamingService service = CreateService(ingestListener, diskRecorder);

        await service.Start();
        ingestListener.Publish(StreamingState.Live, "OBS ingest connected.");
        await WaitFor(() => diskRecorder.StartCount == 1);

        ingestListener.Publish(StreamingState.Error, "RTMP ingest listener failed.");
        await WaitFor(() => diskRecorder.StopCount == 1 && service.State == StreamingState.Error);

        Assert.False(diskRecorder.IsRecording);
    }

    [Fact]
    public async Task WhenStreamingStops_ThenRecorderAndListenerStopAndStateReturnsIdle()
    {
        FakeRtmpIngestListener ingestListener = new();
        FakeDiskRecorder diskRecorder = new();
        StreamingService service = CreateService(ingestListener, diskRecorder);

        await service.Start();
        ingestListener.Publish(StreamingState.Live, "OBS ingest connected.");
        await WaitFor(() => diskRecorder.StartCount == 1);

        await service.Stop();

        Assert.False(service.IsRunning);
        Assert.Equal(StreamingState.Idle, service.State);
        Assert.False(ingestListener.IsListening);
        Assert.Equal(1, ingestListener.StopCount);
        Assert.Equal(1, diskRecorder.StopCount);
    }

    [Fact]
    public async Task WhenIngestListenerStartFails_ThenStreamingMovesToErrorAndIsNotRunning()
    {
        FakeRtmpIngestListener ingestListener = new FakeRtmpIngestListener
        {
            ThrowOnStart = true
        };
        StreamingService service = CreateService(ingestListener, new FakeDiskRecorder());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Start());

        Assert.False(service.IsRunning);
        Assert.Equal(StreamingState.Error, service.State);
    }

    private static StreamingService CreateService(FakeRtmpIngestListener ingestListener, FakeDiskRecorder diskRecorder)
    {
        FakeConfigurationHolder holder = new FakeConfigurationHolder(
            new RtmpServerConfigurationPush(
                "rtmp://localhost:1935/live",
                string.Empty,
                string.Empty,
                Array.Empty<RtmpRelayDestinationPush>()));

        return new StreamingService(
            holder,
            ingestListener,
            diskRecorder,
            NullLogger<StreamingService>.Instance);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private sealed class FakeConfigurationHolder : IRtmpServerConfigurationHolder
    {
        private RtmpServerConfigurationPush _current;

        public FakeConfigurationHolder(RtmpServerConfigurationPush initial)
        {
            _current = initial;
        }

        public RtmpServerConfigurationPush GetCurrent() => _current;

        public void Apply(RtmpServerConfigurationPush configuration)
        {
            _current = configuration;
        }
    }

    private sealed class FakeRtmpIngestListener : IRtmpIngestListener
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public bool ThrowOnStart { get; init; }

        public bool IsListening { get; private set; }

        public event EventHandler<RtmpIngestStateChanged>? StateChanged;

        public Task Start(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;

            if (ThrowOnStart)
            {
                throw new InvalidOperationException("listener failed");
            }

            IsListening = true;
            return Task.CompletedTask;
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            IsListening = false;
            return Task.CompletedTask;
        }

        public void Publish(StreamingState state, string message)
        {
            StateChanged?.Invoke(
                this,
                new RtmpIngestStateChanged
                {
                    State = state,
                    StreamPath = "live",
                    Message = message
                });
        }
    }

    private sealed class FakeDiskRecorder : IDiskRecorder
    {
        public List<Guid?> SessionIds { get; } = [];

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public bool IsRecording { get; private set; }

        public Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            SessionIds.Add(sessionId);
            IsRecording = true;
            return Task.CompletedTask;
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            IsRecording = false;
            return Task.CompletedTask;
        }
    }
}