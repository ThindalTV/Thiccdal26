using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Coordinates the operator go-live workflow without coupling the UI to streaming details.
/// </summary>
public sealed class GoLiveActionService : IGoLiveActionService
{
    private static readonly TimeSpan DefaultGoLiveTimeout = TimeSpan.FromSeconds(30);
    private readonly IStreamingService _streamingService;
    private readonly IRtmpFanoutService _rtmpFanoutService;
    private readonly IOperatorStateService _operatorStateService;
    private readonly IPreLiveChecklistService _preLiveChecklistService;
    private readonly IChecklistSessionService _checklistSessionService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GoLiveActionService> _logger;
    private readonly TimeSpan _goLiveTimeout;
    private readonly Lock _stateLock = new();
    private GoLiveActionState _state = new();

    public GoLiveActionService(
        IStreamingService streamingService,
        IRtmpFanoutService rtmpFanoutService,
        IOperatorStateService operatorStateService,
        IPreLiveChecklistService preLiveChecklistService,
        IChecklistSessionService checklistSessionService,
        TimeProvider timeProvider,
        ILogger<GoLiveActionService> logger)
        : this(
            streamingService,
            rtmpFanoutService,
            operatorStateService,
            preLiveChecklistService,
            checklistSessionService,
            timeProvider,
            logger,
            DefaultGoLiveTimeout)
    {
    }

    public GoLiveActionService(
        IStreamingService streamingService,
        IRtmpFanoutService rtmpFanoutService,
        IOperatorStateService operatorStateService,
        IPreLiveChecklistService preLiveChecklistService,
        IChecklistSessionService checklistSessionService,
        TimeProvider timeProvider,
        ILogger<GoLiveActionService> logger,
        TimeSpan goLiveTimeout)
    {
        ArgumentNullException.ThrowIfNull(streamingService);
        ArgumentNullException.ThrowIfNull(rtmpFanoutService);
        ArgumentNullException.ThrowIfNull(operatorStateService);
        ArgumentNullException.ThrowIfNull(preLiveChecklistService);
        ArgumentNullException.ThrowIfNull(checklistSessionService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(goLiveTimeout, TimeSpan.Zero);

        _streamingService = streamingService;
        _rtmpFanoutService = rtmpFanoutService;
        _operatorStateService = operatorStateService;
        _preLiveChecklistService = preLiveChecklistService;
        _checklistSessionService = checklistSessionService;
        _timeProvider = timeProvider;
        _logger = logger;
        _goLiveTimeout = goLiveTimeout;
    }

    public event EventHandler? StateChanged;

    public GoLiveActionState GetState()
    {
        lock (_stateLock)
        {
            return _state;
        }
    }

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!TryBeginExecution())
        {
            return;
        }

        DateTimeOffset startedAt = _timeProvider.GetUtcNow();
        Guid sessionId = Guid.NewGuid();
        bool streamingStartAttempted = false;
        bool fanoutStarted = false;

        using CancellationTokenSource timeoutSource = new(_goLiveTimeout);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            await _checklistSessionService.Save(sessionId, _preLiveChecklistService, linkedSource.Token);
            streamingStartAttempted = true;
            await _streamingService.Start(linkedSource.Token, sessionId);

            await _rtmpFanoutService.StartFanout(linkedSource.Token);
            fanoutStarted = true;

            _preLiveChecklistService.HandleGoLiveSucceeded(startedAt, sessionId);
            EnsureLiveSessionTransitioned(startedAt, sessionId);
            _preLiveChecklistService.Reset();

            _logger.LogInformation("Stream went live at {StartedAt}", startedAt);
            SetState(new GoLiveActionState());
        }
        catch (Exception ex) when (HandleFailureFilter(ex, cancellationToken, timeoutSource))
        {
            await Cleanup(streamingStartAttempted, fanoutStarted);
        }
    }

    private bool TryBeginExecution()
    {
        bool shouldStart;

        lock (_stateLock)
        {
            shouldStart = !_state.IsRunning;
            if (shouldStart)
            {
                _state = new GoLiveActionState { IsRunning = true };
            }
        }

        if (shouldStart)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        return shouldStart;
    }

    private bool HandleFailureFilter(Exception ex, CancellationToken cancellationToken, CancellationTokenSource timeoutSource)
    {
        if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested && !timeoutSource.IsCancellationRequested)
        {
            SetState(new GoLiveActionState());
            return false;
        }

        string errorMessage = timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested
            ? $"Go live timed out after {(int)_goLiveTimeout.TotalSeconds} seconds. Streaming startup was stopped and the checklist is still available."
            : $"Go live failed: {ex.Message}";

        _logger.LogError(ex, "Go live failed");
        SetState(
            new GoLiveActionState
            {
                ErrorMessage = errorMessage
            });

        return true;
    }

    private async Task Cleanup(bool streamingStartAttempted, bool fanoutStarted)
    {
        if (fanoutStarted)
        {
            try
            {
                await _rtmpFanoutService.StopFanout();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to stop RTMP fanout during go-live cleanup");
            }
        }

        if (streamingStartAttempted)
        {
            try
            {
                await _streamingService.Stop();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to stop streaming during go-live cleanup");
            }
        }
    }

    private void EnsureLiveSessionTransitioned(DateTimeOffset startedAt, Guid sessionId)
    {
        OperatorStreamState? activeStreamState = _operatorStateService.GetActiveStreamState();
        if (_operatorStateService.Mode == OperatorMode.Live && activeStreamState?.SessionId == sessionId)
        {
            return;
        }

        _operatorStateService.BeginLiveSession(startedAt, sessionId);
    }

    private void SetState(GoLiveActionState state)
    {
        lock (_stateLock)
        {
            _state = state;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
