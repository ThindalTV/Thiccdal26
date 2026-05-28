using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Thiccdal.Data;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

/// <summary>
/// Singleton that tracks whether Twitch has a valid stored token.
/// Call <see cref="RefreshConnectionState"/> after any token store/revoke operation
/// so that Blazor components subscribed to <see cref="ConnectionChanged"/> can re-render.
/// </summary>
public sealed class TwitchConnectionMonitor : ITwitchConnectionMonitor
{
    private readonly ITwitchTokenManager _tokenManager;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<TwitchConnectionMonitor> _logger;

    public string PlatformName => "Twitch";

    public bool IsConnected { get; private set; }

    public event EventHandler? ConnectionChanged;

    public TwitchConnectionMonitor(
        ITwitchTokenManager tokenManager,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<TwitchConnectionMonitor> logger)
    {
        _tokenManager = tokenManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public string GetAuthorizationUrl() => _tokenManager.GetAuthorizationUrl();

    public async Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var hasValidToken = await context.TwitchTokens
            .AnyAsync(t => t.ExpiresAt > DateTime.UtcNow, cancellationToken);

        var wasConnected = IsConnected;
        IsConnected = hasValidToken;

        if (wasConnected != IsConnected)
        {
            _logger.LogInformation(
                "Twitch connection state changed to {State}",
                IsConnected ? "connected" : "disconnected");

            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
