using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Instagram;

namespace Thiccdal.Remote.Instagram;

/// <summary>
/// Tracks Instagram Live connection state. Instagram Live requires explicit API approval from Meta.
/// </summary>
public sealed class InstagramConnectionMonitor : IInstagramConnectionMonitor
{
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramConnectionMonitor> _logger;

    public string PlatformName => "Instagram";

    public bool IsConnected => _options.IsEnabled && !string.IsNullOrWhiteSpace(_options.AccessToken);

    public event EventHandler? ConnectionChanged
    {
        add { _ = value; }
        remove { _ = value; }
    }

    public InstagramConnectionMonitor(
        IOptions<InstagramOptions> options,
        ILogger<InstagramConnectionMonitor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GetAuthorizationUrl()
    {
        _logger.LogInformation("Instagram Live authorization URL requested (requires API approval first)");
        return string.Empty;
    }

    public Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsEnabled)
        {
            _logger.LogDebug("Instagram Live is disabled; awaiting API approval from Meta");
            return Task.CompletedTask;
        }

        var hasValidConfig = !string.IsNullOrWhiteSpace(_options.AccessToken) &&
                            !string.IsNullOrWhiteSpace(_options.BroadcasterId);

        if (!hasValidConfig)
        {
            _logger.LogWarning(
                "Instagram Live is enabled but not fully configured. AccessToken: {HasAccessToken}, BroadcasterId: {HasBroadcasterId}",
                !string.IsNullOrWhiteSpace(_options.AccessToken),
                !string.IsNullOrWhiteSpace(_options.BroadcasterId));
        }

        return Task.CompletedTask;
    }
}
