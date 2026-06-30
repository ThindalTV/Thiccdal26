using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Owns the local FFmpeg recording lifecycle for the standalone RTMP server process.
/// Recording rows are not persisted to a database — the process is stateless.
/// </summary>
public sealed class DiskRecorder : IDiskRecorder
{
    private const string FileExtension = ".mkv";

    private readonly IRtmpServerConfigurationHolder _holder;
    private readonly IOptions<RtmpServerOptions> _rtmpServerOptions;
    private readonly IRecordingProcessRunner _processRunner;
    private readonly ILogger<DiskRecorder> _logger;
    private readonly Lock _stateLock = new();
    private IRecordingProcess? _currentProcess;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskRecorder"/> class.
    /// </summary>
    public DiskRecorder(
        IRtmpServerConfigurationHolder holder,
        IOptions<RtmpServerOptions> rtmpServerOptions,
        IRecordingProcessRunner processRunner,
        ILogger<DiskRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(rtmpServerOptions);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(logger);

        _holder = holder;
        _rtmpServerOptions = rtmpServerOptions;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsRecording
    {
        get
        {
            lock (_stateLock)
            {
                return _currentProcess is not null && !_currentProcess.HasExited;
            }
        }
    }

    /// <inheritdoc />
    public Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateLock)
        {
            if (_currentProcess is not null && !_currentProcess.HasExited)
            {
                return Task.CompletedTask;
            }
        }

        RtmpServerConfigurationPush config = _holder.GetCurrent();
        string ingestUrl = NormalizeRequired(config.IngestUrl, "IngestUrl must be configured before recording can start.");
        string recordingOutputPath = NormalizeRequired(config.RecordingOutputPath, "RecordingOutputPath must be configured before recording can start.");
        string executablePath = NormalizeRequired(_rtmpServerOptions.Value.FfmpegExecutablePath, "RtmpServer:FfmpegExecutablePath must be configured before recording can start.");
        string fullOutputDirectory = Path.GetFullPath(recordingOutputPath);

        Directory.CreateDirectory(fullOutputDirectory);

        string filePath = Path.Combine(fullOutputDirectory, BuildFileName(sessionId));

        RecordingProcessRequest request = new RecordingProcessRequest
        {
            ExecutablePath = executablePath,
            IngestUrl = ingestUrl,
            OutputPath = filePath
        };

        IRecordingProcess process = _processRunner.Start(request, cancellationToken);
        process.Exited += OnProcessExited;

        lock (_stateLock)
        {
            _currentProcess = process;
        }

        _logger.LogInformation(
            "Started disk recording for session {SessionId} at {FilePath}.",
            sessionId,
            filePath);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task Stop(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IRecordingProcess? process;

        lock (_stateLock)
        {
            process = _currentProcess;
            if (process is null || process.HasExited)
            {
                return;
            }
        }

        try
        {
            await process.Stop(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CleanupCurrentProcess();
            _logger.LogError(ex, "Disk recording stop failed.");
            throw new InvalidOperationException($"Disk recording could not stop cleanly: {ex.Message}", ex);
        }
    }

    private static string BuildFileName(Guid? sessionId)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        string sessionSegment = sessionId?.ToString("N") ?? "manual";
        return $"thiccdal-{timestamp}-{sessionSegment}{FileExtension}";
    }

    private static string NormalizeRequired(string? value, string message)
    {
        string normalizedValue = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new InvalidOperationException(message);
        }

        return normalizedValue;
    }

    private void OnProcessExited(object? sender, EventArgs args)
    {
        _ = args;
        _ = sender;
        CleanupCurrentProcess();
    }

    private void CleanupCurrentProcess()
    {
        IRecordingProcess? process;

        lock (_stateLock)
        {
            process = _currentProcess;
            _currentProcess = null;
        }

        process?.Dispose();
    }
}
