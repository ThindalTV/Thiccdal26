using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Bridges <see cref="IStreamingService.StateChanged"/> events to SignalR clients via <see cref="RtmpEventPublisher"/>.
/// </summary>
public sealed class RtmpEventBridgeService : IHostedService
{
    private readonly IStreamingService _streamingService;
    private readonly IRtmpEventPublisher _eventPublisher;
    private readonly ILogger<RtmpEventBridgeService> _logger;
    private StreamingState _previousState = StreamingState.Idle;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtmpEventBridgeService"/> class.
    /// </summary>
    public RtmpEventBridgeService(IStreamingService streamingService, IRtmpEventPublisher eventPublisher, ILogger<RtmpEventBridgeService> logger)
    {
        ArgumentNullException.ThrowIfNull(streamingService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(logger);

        _streamingService = streamingService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _streamingService.StateChanged += OnStateChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _streamingService.StateChanged -= OnStateChanged;
        return Task.CompletedTask;
    }

    private void OnStateChanged(object? sender, StreamingState state)
    {
        _ = sender;
        Task publish = PublishTransition(state);
        publish.ContinueWith(
            t => _logger.LogError(t.Exception, "Unhandled error publishing RTMP state transition {State}.", state),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task PublishTransition(StreamingState state)
    {
        StreamingState previous = _previousState;
        _previousState = state;

        switch (state)
        {
            case StreamingState.Live:
                await _eventPublisher.PublishIngestConnected(string.Empty);
                await _eventPublisher.PublishRecordingStarted();
                break;
            case StreamingState.BrbSlate:
                if (previous == StreamingState.Live)
                {
                    await _eventPublisher.PublishRecordingEnded();
                }
                await _eventPublisher.PublishIngestDisconnected(string.Empty);
                break;
            case StreamingState.Error:
                if (previous == StreamingState.Live)
                {
                    await _eventPublisher.PublishRecordingEnded();
                }
                await _eventPublisher.PublishIngestError("Streaming entered error state.");
                break;
        }
    }
}
