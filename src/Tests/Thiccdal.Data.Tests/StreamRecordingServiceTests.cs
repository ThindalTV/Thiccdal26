using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Data.Tests;

public sealed class StreamRecordingServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenRecordingStartsAndStops_ThenLifecycleIsPersisted()
    {
        FixedTimeProvider timeProvider = new(
            new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 19, 0, 0, TimeSpan.Zero));
        StreamRecordingService service = new(
            DbContextFactory,
            timeProvider,
            NullLogger<StreamRecordingService>.Instance);
        Guid sessionId = Guid.NewGuid();

        StreamRecordingSnapshot started = await service.Start(
            sessionId,
            "Local",
            @"C:\Recordings\phase8.mkv");
        StreamRecordingSnapshot stopped = await service.Stop(started.Id, null);
        StreamRecordingSnapshot latest = Assert.IsType<StreamRecordingSnapshot>(await service.GetLatest("Local"));

        Assert.Equal(sessionId, started.SessionId);
        Assert.Equal("Local", started.Platform);
        Assert.Equal(@"C:\Recordings\phase8.mkv", started.FilePath);
        Assert.Equal(timeProvider.StartedAt, started.StartedAt);
        Assert.Null(started.EndedAt);
        Assert.Equal(started.Id, stopped.Id);
        Assert.Equal(timeProvider.StoppedAt, stopped.EndedAt);
        Assert.Equal(string.Empty, stopped.Error);
        Assert.Equal(stopped, latest);
    }

    [Fact]
    public async Task WhenRecordingStopsWithFailure_ThenErrorIsPersisted()
    {
        FixedTimeProvider timeProvider = new(
            new DateTimeOffset(2026, 6, 2, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 18, 5, 0, TimeSpan.Zero));
        StreamRecordingService service = new(
            DbContextFactory,
            timeProvider,
            NullLogger<StreamRecordingService>.Instance);

        StreamRecordingSnapshot started = await service.Start(
            null,
            "Local",
            @"C:\Recordings\phase8-failed.mkv");
        StreamRecordingSnapshot stopped = await service.Stop(started.Id, "FFmpeg exited with code 1.");

        Assert.Equal("FFmpeg exited with code 1.", stopped.Error);
        Assert.Equal(timeProvider.StoppedAt, stopped.EndedAt);
    }

    [Fact]
    public async Task WhenMultipleRecordingsExist_ThenGetLatestReturnsNewestTrimmedRowForPlatform()
    {
        FixedTimeProvider timeProvider = new(
            new DateTimeOffset(2026, 6, 3, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 3, 18, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 3, 19, 0, 0, TimeSpan.Zero));
        StreamRecordingService service = new(
            DbContextFactory,
            timeProvider,
            NullLogger<StreamRecordingService>.Instance);

        await service.Start(null, " Twitch ", @" C:\Recordings\older.mkv ");
        StreamRecordingSnapshot latestStarted = await service.Start(null, "Twitch", @"C:\Recordings\latest.mkv");

        StreamRecordingSnapshot latest = Assert.IsType<StreamRecordingSnapshot>(await service.GetLatest("Twitch"));

        Assert.Equal(latestStarted.Id, latest.Id);
        Assert.Equal("Twitch", latest.Platform);
        Assert.Equal(@"C:\Recordings\latest.mkv", latest.FilePath);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly Queue<DateTimeOffset> _timestamps;

        public FixedTimeProvider(params DateTimeOffset[] timestamps)
        {
            StartedAt = timestamps[0];
            StoppedAt = timestamps[^1];
            _timestamps = new Queue<DateTimeOffset>(timestamps);
        }

        public DateTimeOffset StartedAt { get; }

        public DateTimeOffset StoppedAt { get; }

        public override DateTimeOffset GetUtcNow()
        {
            return _timestamps.Count > 0 ? _timestamps.Dequeue() : StoppedAt;
        }
    }
}
