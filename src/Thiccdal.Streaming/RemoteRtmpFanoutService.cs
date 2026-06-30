using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

/// <summary>
/// Implements <see cref="IRtmpFanoutService"/> as a thin wrapper over the remote RTMP server.
/// Actual fanout is handled by the remote process after configuration is pushed via <see cref="IRtmpServerClient"/>.
/// </summary>
public sealed class RemoteRtmpFanoutService : IRtmpFanoutService
{
    private readonly ILogger<RemoteRtmpFanoutService> _logger;
    private readonly Lock _stateLock = new();
    private bool _isRunning;

    /// <summary>
    /// Initializes a new instance of <see cref="RemoteRtmpFanoutService"/>.
    /// </summary>
    public RemoteRtmpFanoutService(ILogger<RemoteRtmpFanoutService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsRunning
    {
        get
        {
            lock (_stateLock)
            {
                return _isRunning;
            }
        }
    }

    /// <inheritdoc/>
    public int ActiveRelayCount => 0;

    /// <inheritdoc/>
    public Task StartFanout(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateLock)
        {
            _isRunning = true;
        }

        _logger.LogInformation("Marked remote RTMP fanout as started.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopFanout(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateLock)
        {
            _isRunning = false;
        }

        _logger.LogInformation("Marked remote RTMP fanout as stopped.");
        return Task.CompletedTask;
    }
}
