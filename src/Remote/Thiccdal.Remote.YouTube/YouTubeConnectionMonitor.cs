using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.Remote.YouTube;

public sealed class YouTubeConnectionMonitor : IYouTubeConnectionMonitor
{
    private readonly IYouTubeTokenManager _tokenManager;
    private readonly IYouTubeTokenStore _tokenStore;
    private readonly ILogger<YouTubeConnectionMonitor> _logger;

    public string PlatformName => "YouTube";

    public bool IsConnected { get; private set; }

    public event EventHandler? ConnectionChanged;

    public YouTubeConnectionMonitor(
        IYouTubeTokenManager tokenManager,
        IYouTubeTokenStore tokenStore,
        ILogger<YouTubeConnectionMonitor> logger)
    {
        _tokenManager = tokenManager;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public string GetAuthorizationUrl() => _tokenManager.GetAuthorizationUrl();

    public async Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        bool hasValidToken = await _tokenStore.HasValidToken(DateTime.UtcNow, cancellationToken);

        var wasConnected = IsConnected;
        IsConnected = hasValidToken;

        if (wasConnected != IsConnected)
        {
            _logger.LogInformation(
                "YouTube connection state changed to {State}",
                IsConnected ? "connected" : "disconnected");

            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
