using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Data;

public sealed class StreamRecordingService : IStreamRecordingService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StreamRecordingService> _logger;

    public StreamRecordingService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        TimeProvider timeProvider,
        ILogger<StreamRecordingService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<StreamRecordingSnapshot> Start(
        Guid? sessionId,
        string platform,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        string normalizedPlatform = NormalizePlatform(platform);
        string normalizedFilePath = NormalizeFilePath(filePath);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        StreamRecording recording = new StreamRecording
        {
            SessionId = sessionId,
            Platform = normalizedPlatform,
            FilePath = normalizedFilePath,
            StartedAt = _timeProvider.GetUtcNow(),
            Error = string.Empty
        };

        dbContext.StreamRecordings.Add(recording);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Persisted stream recording start for platform {Platform} at {FilePath}.",
            normalizedPlatform,
            normalizedFilePath);

        return Map(recording);
    }

    public async Task<StreamRecordingSnapshot> Stop(
        int recordingId,
        string? error,
        CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        StreamRecording recording = await dbContext.StreamRecordings
            .SingleAsync(current => current.Id == recordingId, cancellationToken);

        recording.EndedAt = _timeProvider.GetUtcNow();
        recording.Error = (error ?? string.Empty).Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(recording.Error))
        {
            _logger.LogInformation(
                "Persisted clean stream recording stop for platform {Platform} at {FilePath}.",
                recording.Platform,
                recording.FilePath);
        }
        else
        {
            _logger.LogWarning(
                "Persisted stream recording failure for platform {Platform} at {FilePath}: {Error}",
                recording.Platform,
                recording.FilePath,
                recording.Error);
        }

        return Map(recording);
    }

    public async Task<StreamRecordingSnapshot?> GetLatest(string platform, CancellationToken cancellationToken = default)
    {
        string normalizedPlatform = NormalizePlatform(platform);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        StreamRecording? recording = await dbContext.StreamRecordings
            .AsNoTracking()
            .Where(current => current.Platform == normalizedPlatform)
            .OrderByDescending(current => current.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return recording is null ? null : Map(recording);
    }

    private static string NormalizePlatform(string platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            throw new ArgumentException("A recording platform is required.", nameof(platform));
        }

        return platform.Trim();
    }

    private static string NormalizeFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A recording file path is required.", nameof(filePath));
        }

        return filePath.Trim();
    }

    private static StreamRecordingSnapshot Map(StreamRecording recording)
    {
        return new StreamRecordingSnapshot
        {
            Id = recording.Id,
            SessionId = recording.SessionId,
            Platform = recording.Platform,
            FilePath = recording.FilePath,
            StartedAt = recording.StartedAt,
            EndedAt = recording.EndedAt,
            Error = recording.Error
        };
    }
}
