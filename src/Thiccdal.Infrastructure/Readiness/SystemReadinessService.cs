using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Infrastructure.Readiness;

/// <summary>
/// Derives surface readiness from the saved Twitch target channel and stored authorization.
/// </summary>
public sealed class SystemReadinessService : ISystemReadinessService, IDisposable
{
    private readonly ITwitchTargetChannelService _targetChannelService;
    private readonly ITwitchTokenManager _tokenManager;
    private readonly ILogger<SystemReadinessService> _logger;

    public SystemReadinessService(
        ITwitchTargetChannelService targetChannelService,
        ITwitchTokenManager tokenManager,
        ILogger<SystemReadinessService> logger)
    {
        ArgumentNullException.ThrowIfNull(targetChannelService);
        ArgumentNullException.ThrowIfNull(tokenManager);
        ArgumentNullException.ThrowIfNull(logger);

        _targetChannelService = targetChannelService;
        _tokenManager = tokenManager;
        _logger = logger;

        _targetChannelService.ConnectionProfileChanged += HandleConnectionProfileChanged;
    }

    public event EventHandler? ReadinessChanged;

    public async Task<SystemReadiness> GetReadiness(CancellationToken cancellationToken = default)
    {
        bool hasChannel = false;
        bool hasTwitchAuth = false;

        try
        {
            TwitchChatConnectionProfile profile = await _targetChannelService.GetConnectionProfile(cancellationToken);
            hasChannel = !string.IsNullOrWhiteSpace(profile.TargetChannel);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An unreadable channel setting means "not configured yet", not a surface failure.
            _logger.LogWarning(ex, "Failed to resolve the Twitch target channel while checking readiness");
        }

        try
        {
            hasTwitchAuth = await _tokenManager.HasToken(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to check for a stored Twitch token while checking readiness");
        }

        return new SystemReadiness
        {
            HasChannel = hasChannel,
            HasTwitchAuth = hasTwitchAuth
        };
    }

    /// <summary>
    /// Notifies subscribers that configuration changed outside the target-channel event.
    /// </summary>
    public void NotifyChanged()
    {
        ReadinessChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _targetChannelService.ConnectionProfileChanged -= HandleConnectionProfileChanged;
        GC.SuppressFinalize(this);
    }

    private void HandleConnectionProfileChanged(object? sender, TwitchChatConnectionProfile profile)
    {
        _ = profile;
        ReadinessChanged?.Invoke(this, EventArgs.Empty);
    }
}
