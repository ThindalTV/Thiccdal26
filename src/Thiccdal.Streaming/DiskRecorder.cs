using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

/// <summary>
/// Owns the local FFmpeg recording lifecycle and its persisted database row.
/// </summary>
public sealed class DiskRecorder : IDiskRecorder
{
    private const string LocalRecordingPlatform = "Local";
    private const string FileExtension = ".mkv";

    private readonly IOptions<StreamingOptions> _options;
    private readonly IRecordingProcessRunner _processRunner;
    private readonly IStreamRecordingService _streamRecordingService;
    private readonly ILogger<DiskRecorder> _logger;
    private readonly Lock _stateLock = new();
    private IRecordingProcess? _currentProcess;
    private int? _activeRecordingId;
    private bool _stopRequested;

    public DiskRecorder(
        IOptions<StreamingOptions> options,
        IRecordingProcessRunner processRunner,
        IStreamRecordingService streamRecordingService,
        ILogger<DiskRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(streamRecordingService);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _processRunner = processRunner;
        _streamRecordingService = streamRecordingService;
        _logger = logger;
    }

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

    public async Task Start(CancellationToken cancellationToken = default, Guid? sessionId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateLock)
        {
            if (_currentProcess is not null && !_currentProcess.HasExited)
            {
                return;
            }
        }

        string ingestUrl = NormalizeRequired(_options.Value.IngestUrl, "Streaming:IngestUrl must be configured before recording can start.");
        string recordingOutputPath = NormalizeRequired(_options.Value.RecordingOutputPath, "Streaming:RecordingOutputPath must be configured before recording can start.");
        string executablePath = NormalizeRequired(_options.Value.FfmpegExecutablePath, "Streaming:FfmpegExecutablePath must be configured before recording can start.");
        string fullOutputDirectory = Path.GetFullPath(recordingOutputPath);

        Directory.CreateDirectory(fullOutputDirectory);

        string filePath = Path.Combine(fullOutputDirectory, BuildFileName(sessionId));
        StreamRecordingSnapshot recording = await _streamRecordingService.Start(sessionId, LocalRecordingPlatform, filePath, cancellationToken);

        try
        {
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
                _activeRecordingId = recording.Id;
                _stopRequested = false;
            }

            _logger.LogInformation(
                "Started disk recording for session {SessionId} at {FilePath}.",
                sessionId,
                filePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _streamRecordingService.Stop(recording.Id, ex.Message, cancellationToken);
            _logger.LogError(ex, "Disk recording failed to start for {FilePath}.", filePath);
            throw new InvalidOperationException($"Disk recording could not start: {ex.Message}", ex);
        }
    }

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

            _stopRequested = true;
        }

        try
        {
            await process.Stop(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await CompleteCurrentRecording(ex.Message, cancellationToken);
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
        _ = HandleProcessExit(sender as IRecordingProcess);
    }

    private async Task HandleProcessExit(IRecordingProcess? exitedProcess)
    {
        if (exitedProcess is null)
        {
            return;
        }

        string? error = null;

        lock (_stateLock)
        {
            if (!ReferenceEquals(_currentProcess, exitedProcess) || _activeRecordingId is null)
            {
                return;
            }

            if (exitedProcess.ExitCode != 0)
            {
                error = _stopRequested
                    ? $"Recording process exited with code {exitedProcess.ExitCode} during shutdown."
                    : $"Recording process exited unexpectedly with code {exitedProcess.ExitCode}.";
            }
        }

        await CompleteCurrentRecording(error, CancellationToken.None);
    }

    private async Task CompleteCurrentRecording(string? error, CancellationToken cancellationToken)
    {
        IRecordingProcess? process;
        int? recordingId;

        lock (_stateLock)
        {
            process = _currentProcess;
            recordingId = _activeRecordingId;
            _currentProcess = null;
            _activeRecordingId = null;
            _stopRequested = false;
        }

        if (recordingId is null)
        {
            process?.Dispose();
            return;
        }

        try
        {
            await _streamRecordingService.Stop(recordingId.Value, error, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to persist recording completion for row {RecordingId}.", recordingId.Value);
        }
        finally
        {
            process?.Dispose();
        }
    }
}
