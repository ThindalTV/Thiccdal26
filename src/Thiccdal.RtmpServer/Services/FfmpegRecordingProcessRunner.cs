using System.Diagnostics;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Starts and stops the FFmpeg process used for local recordings.
/// </summary>
public sealed class FfmpegRecordingProcessRunner : IRecordingProcessRunner
{
    private static readonly TimeSpan GracefulShutdownTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public IRecordingProcess Start(RecordingProcessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("warning");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(request.IngestUrl);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("copy");
        startInfo.ArgumentList.Add(request.OutputPath);

        Process process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Failed to launch recording process '{request.ExecutablePath}'.");
        }

        return new ProcessRecordingProcess(process);
    }

    private sealed class ProcessRecordingProcess : IRecordingProcess
    {
        private readonly Process _process;

        public ProcessRecordingProcess(Process process)
        {
            ArgumentNullException.ThrowIfNull(process);

            _process = process;
            _process.Exited += HandleProcessExited;
        }

        public event EventHandler? Exited;

        public bool HasExited => _process.HasExited;

        public int ExitCode => _process.HasExited ? _process.ExitCode : 0;

        public async Task Stop(CancellationToken cancellationToken = default)
        {
            if (_process.HasExited)
            {
                return;
            }

            await _process.StandardInput.WriteLineAsync("q");
            await _process.StandardInput.FlushAsync(cancellationToken);

            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(GracefulShutdownTimeout);

            try
            {
                await _process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }

                await _process.WaitForExitAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            _process.Exited -= HandleProcessExited;
            _process.Dispose();
        }

        private void HandleProcessExited(object? sender, EventArgs args)
        {
            _ = sender;
            Exited?.Invoke(this, args);
        }
    }
}
