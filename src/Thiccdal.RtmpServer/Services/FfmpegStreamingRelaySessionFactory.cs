using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Launches FFmpeg processes to relay live or BRB streams to downstream platforms.
/// </summary>
public sealed class FfmpegStreamingRelaySessionFactory : IStreamingRelaySessionFactory
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".gif",
        ".jpeg",
        ".jpg",
        ".png",
        ".webp"
    };

    private readonly IOptions<RtmpServerOptions> _rtmpServerOptions;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegStreamingRelaySessionFactory"/> class.
    /// </summary>
    public FfmpegStreamingRelaySessionFactory(IOptions<RtmpServerOptions> rtmpServerOptions, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(rtmpServerOptions);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _rtmpServerOptions = rtmpServerOptions;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public Task<IStreamingRelaySession> StartLiveRelay(
        string platformName,
        string sourceUrl,
        string destinationUrl,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IStreamingRelaySession>(
            StartProcess(platformName, BuildLiveArguments(sourceUrl, destinationUrl), cancellationToken));
    }

    /// <inheritdoc />
    public Task<IStreamingRelaySession> StartBrbRelay(
        string platformName,
        string slatePath,
        string destinationUrl,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IStreamingRelaySession>(
            StartProcess(platformName, BuildBrbArguments(slatePath, destinationUrl), cancellationToken));
    }

    private IStreamingRelaySession StartProcess(string platformName, string arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _rtmpServerOptions.Value.FfmpegExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Failed to launch FFmpeg relay for {platformName}.");
        }

        return new ProcessBackedStreamingRelaySession(
            platformName,
            process,
            _loggerFactory.CreateLogger<ProcessBackedStreamingRelaySession>());
    }

    private static string BuildLiveArguments(string sourceUrl, string destinationUrl)
    {
        return string.Join(
            ' ',
            [
                "-nostdin",
                "-hide_banner",
                "-loglevel warning",
                $"-i \"{sourceUrl}\"",
                "-c copy",
                $"-f flv \"{destinationUrl}\""
            ]);
    }

    private static string BuildBrbArguments(string slatePath, string destinationUrl)
    {
        bool isImage = ImageExtensions.Contains(Path.GetExtension(slatePath));
        string inputFlags = isImage
            ? $"-loop 1 -re -i \"{slatePath}\" -f lavfi -i anullsrc=channel_layout=stereo:sample_rate=48000"
            : $"-stream_loop -1 -re -i \"{slatePath}\"";
        string codecFlags = isImage
            ? "-shortest -c:v libx264 -pix_fmt yuv420p -c:a aac -b:a 128k"
            : "-c:v libx264 -pix_fmt yuv420p -c:a aac -b:a 128k";

        return string.Join(
            ' ',
            [
                "-nostdin",
                "-hide_banner",
                "-loglevel warning",
                inputFlags,
                codecFlags,
                $"-f flv \"{destinationUrl}\""
            ]);
    }

    private sealed class ProcessBackedStreamingRelaySession : IStreamingRelaySession
    {
        private readonly Process _process;
        private readonly ILogger<ProcessBackedStreamingRelaySession> _logger;

        public ProcessBackedStreamingRelaySession(
            string platformName,
            Process process,
            ILogger<ProcessBackedStreamingRelaySession> logger)
        {
            PlatformName = platformName;
            _process = process;
            _logger = logger;
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public string PlatformName { get; }

        public async Task Stop(CancellationToken cancellationToken = default)
        {
            if (_process.HasExited)
            {
                return;
            }

            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync();
                }
                catch (InvalidOperationException)
                {
                }
            }

            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.ErrorDataReceived -= OnErrorDataReceived;
            _process.Dispose();
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs args)
        {
            _ = sender;
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _logger.LogDebug("FFmpeg relay {PlatformName}: {Message}", PlatformName, args.Data);
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs args)
        {
            _ = sender;
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _logger.LogDebug("FFmpeg relay {PlatformName}: {Message}", PlatformName, args.Data);
            }
        }
    }
}
