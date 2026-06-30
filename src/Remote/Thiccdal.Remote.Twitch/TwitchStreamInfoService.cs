using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public sealed class TwitchStreamInfoService : BackgroundService, ITwitchStreamInfoService
{
    private readonly ITwitchHelixClient _helixClient;
    private readonly ITwitchTargetChannelService _targetChannelService;
    private readonly ITwitchTokenManager _tokenManager;
    private readonly ILogger<TwitchStreamInfoService> _logger;
    private readonly TwitchHelixOptions _options;

    private TwitchStreamState? _currentState;

    public TwitchStreamState? CurrentState => _currentState;

    public event EventHandler<TwitchStreamState?>? StreamStateChanged;

    public TwitchStreamInfoService(
        ITwitchHelixClient helixClient,
        ITwitchTargetChannelService targetChannelService,
        ITwitchTokenManager tokenManager,
        ILogger<TwitchStreamInfoService> logger,
        IOptions<TwitchHelixOptions> options)
    {
        _helixClient = helixClient;
        _targetChannelService = targetChannelService;
        _tokenManager = tokenManager;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.StreamStateRefreshSeconds), stoppingToken);

            try
            {
                string? token = await _tokenManager.GetToken(stoppingToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                TwitchChatConnectionProfile profile = await _targetChannelService.GetConnectionProfile(stoppingToken);
                TwitchStreamState newState = await _helixClient.GetStreamState(profile, stoppingToken);

                if (HasStateChanged(newState))
                {
                    _currentState = newState;
                    StreamStateChanged?.Invoke(this, _currentState);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh Twitch stream state");
            }
        }
    }

    private bool HasStateChanged(TwitchStreamState newState)
    {
        if (_currentState is null)
        {
            return true;
        }

        return _currentState.IsLive != newState.IsLive
            || _currentState.Title != newState.Title
            || _currentState.ViewerCount != newState.ViewerCount;
    }
}
