using Microsoft.Extensions.Hosting;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Services;

/// <summary>
/// Bridges <see cref="IStreamingService.StateChanged"/> events to SignalR clients via <see cref="RtmpEventPublisher"/>.
/// </summary>
public sealed class RtmpEventBridgeService : IHostedService
{
    private readonly IStreamingService _streamingService;
    private readonly IRtmpEventPublisher _eventPublisher;
    private StreamingState _previousState = StreamingState.Idle;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtmpEventBridgeService"/> class.
    /// </summary>
    public RtmpEventBridgeService(IStreamingService streamingService, IRtmpEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(streamingService);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _streamingService = streamingService;
        _eventPublisher = eventPublisher;
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
        _ = PublishTransition(state);
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
