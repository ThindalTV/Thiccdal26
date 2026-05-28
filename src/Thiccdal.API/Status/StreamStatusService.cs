using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.API.Status;

/// <summary>
/// Default implementation that adapts the current host service state into the public status contract.
/// </summary>
public sealed class StreamStatusService : IStreamStatusService
{
    private readonly IOperatorStateService _operatorStateService;
    private readonly ITwitchService _twitchService;
    private readonly IYouTubeService _youTubeService;
    private readonly IReadOnlyList<IPlatformConnection> _platformConnections;
    private readonly ILogger<StreamStatusService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamStatusService"/> class.
    /// </summary>
    /// <param name="operatorStateService">The shared operator state service.</param>
    /// <param name="twitchService">The Twitch integration service.</param>
    /// <param name="youTubeService">The YouTube integration service.</param>
    /// <param name="platformConnections">All registered platform connections.</param>
    /// <param name="logger">The logger.</param>
    public StreamStatusService(
        IOperatorStateService operatorStateService,
        ITwitchService twitchService,
        IYouTubeService youTubeService,
        IEnumerable<IPlatformConnection> platformConnections,
        ILogger<StreamStatusService> logger)
    {
        _operatorStateService = operatorStateService;
        _twitchService = twitchService;
        _youTubeService = youTubeService;
        _platformConnections = platformConnections.ToArray();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StreamStatusResponse> GetStatus(CancellationToken cancellationToken = default)
    {
        await RefreshPlatformState(cancellationToken);

        IReadOnlyList<PlatformStatusDto> platforms = BuildPlatformStatuses();
        StreamInfoDto? stream = BuildStream();

        if (stream is null)
        {
            _operatorStateService.SetActiveStreamState(null);

            if (_operatorStateService.Mode != OperatorMode.PreLive)
            {
                _operatorStateService.SetMode(OperatorMode.PreLive);
            }
        }

        return new StreamStatusResponse
        {
            State = stream is null ? StreamStatusStates.Offline : StreamStatusStates.Online,
            Stream = stream,
            Platforms = platforms
        };
    }

    private async Task RefreshPlatformState(CancellationToken cancellationToken)
    {
        IEnumerable<Task> refreshOperations = _platformConnections
            .Select(platformConnection =>
                RefreshSafely(
                    () => platformConnection.RefreshConnectionState(cancellationToken),
                    $"{platformConnection.PlatformName} connection state"))
            .Concat(
            [
                RefreshSafely(() => _twitchService.RefreshStreamState(cancellationToken), "Twitch stream state"),
                RefreshSafely(() => _youTubeService.RefreshStreamState(cancellationToken), "YouTube stream state")
            ]);

        await Task.WhenAll(refreshOperations);
    }

    private async Task RefreshSafely(Func<Task> refresh, string operationName)
    {
        try
        {
            await refresh();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to refresh {OperationName}", operationName);
        }
    }

    private StreamInfoDto? BuildStream()
    {
        OperatorStreamState? activeStreamState = _operatorStateService.GetActiveStreamState();
        if (activeStreamState is not null)
        {
            if (_operatorStateService.Mode != OperatorMode.Live)
            {
                _operatorStateService.SetMode(OperatorMode.Live);
            }

            return CreateStreamDto(activeStreamState);
        }

        if (_twitchService.StreamState.IsLive)
        {
            OperatorStreamState streamState = new()
            {
                Title = _twitchService.StreamState.Title,
                Category = _twitchService.StreamState.Category,
                Tags = _twitchService.StreamState.Tags,
                StartedAt = _twitchService.StreamState.StartedAt
            };
            _operatorStateService.SetActiveStreamState(streamState);

            if (_operatorStateService.Mode != OperatorMode.Live)
            {
                _operatorStateService.SetMode(OperatorMode.Live);
            }

            return CreateStreamDto(streamState);
        }

        if (_youTubeService.ActiveBroadcast?.IsLive == true)
        {
            OperatorStreamState streamState = new()
            {
                Title = _youTubeService.ActiveBroadcast.Title,
                Category = _youTubeService.ActiveBroadcast.Category,
                Tags = _youTubeService.ActiveBroadcast.Tags,
                StartedAt = _youTubeService.ActiveBroadcast.StartedAt
            };
            _operatorStateService.SetActiveStreamState(streamState);

            if (_operatorStateService.Mode != OperatorMode.Live)
            {
                _operatorStateService.SetMode(OperatorMode.Live);
            }

            return CreateStreamDto(streamState);
        }

        return null;
    }

    private IReadOnlyList<PlatformStatusDto> BuildPlatformStatuses()
    {
        return _platformConnections
            .Where(static platformConnection => platformConnection.State != PlatformConnectionState.Disabled)
            .Select(MapPlatformStatus)
            .OrderBy(static platform => platform.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static PlatformStatusDto MapPlatformStatus(IPlatformConnection platformConnection)
    {
        return new PlatformStatusDto
        {
            Name = platformConnection.PlatformName,
            State = platformConnection.State.ToString(),
            Error = platformConnection.State == PlatformConnectionState.Error
                ? platformConnection.LastError
                : null
        };
    }

    private static StreamInfoDto CreateStreamDto(OperatorStreamState streamState)
    {
        TimeSpan uptime = streamState.StartedAt.HasValue
            ? DateTimeOffset.UtcNow - streamState.StartedAt.Value
            : TimeSpan.Zero;

        if (uptime < TimeSpan.Zero)
        {
            uptime = TimeSpan.Zero;
        }

        return new StreamInfoDto
        {
            Title = streamState.Title,
            Category = streamState.Category,
            Tags = streamState.Tags,
            StartedAt = streamState.StartedAt ?? DateTimeOffset.UtcNow,
            Uptime = uptime.ToString(@"hh\:mm\:ss")
        };
    }
}
