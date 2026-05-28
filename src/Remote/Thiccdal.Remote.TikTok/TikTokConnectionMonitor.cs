using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.TikTok;

namespace Thiccdal.Remote.TikTok;

/// <summary>
/// Tracks TikTok Live connection state. TikTok Live requires explicit API approval from TikTok.
/// </summary>
public sealed class TikTokConnectionMonitor : ITikTokConnectionMonitor
{
    private readonly TikTokOptions _options;
    private readonly ILogger<TikTokConnectionMonitor> _logger;

    public string PlatformName => "TikTok";

    public bool IsConnected => _options.IsEnabled && !string.IsNullOrWhiteSpace(_options.AccessToken);

    public event EventHandler? ConnectionChanged
    {
        add { _ = value; }
        remove { _ = value; }
    }

    public TikTokConnectionMonitor(
        IOptions<TikTokOptions> options,
        ILogger<TikTokConnectionMonitor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GetAuthorizationUrl()
    {
        _logger.LogInformation("TikTok Live authorization URL requested (requires API approval first)");
        return string.Empty;
    }

    public Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsEnabled)
        {
            _logger.LogDebug("TikTok Live is disabled; awaiting API approval from TikTok");
            return Task.CompletedTask;
        }

        var hasValidConfig = !string.IsNullOrWhiteSpace(_options.AccessToken) &&
                            !string.IsNullOrWhiteSpace(_options.CreatorId);

        if (!hasValidConfig)
        {
            _logger.LogWarning(
                "TikTok Live is enabled but not fully configured. AccessToken: {HasAccessToken}, CreatorId: {HasCreatorId}",
                !string.IsNullOrWhiteSpace(_options.AccessToken),
                !string.IsNullOrWhiteSpace(_options.CreatorId));
        }

        return Task.CompletedTask;
    }
}
