using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Facebook;
using Thiccdal.Infrastructure.Integrations;

namespace Thiccdal.Remote.Facebook;

public class FacebookConnectionMonitor : IFacebookConnectionMonitor, IIntegrationConnectionMonitor, IHostedService
{
    private readonly IFacebookService _facebookService;
    private readonly ILogger<FacebookConnectionMonitor> _logger;

    public string PlatformName => "Facebook";
    public bool IsConnected { get; private set; }

    public event EventHandler? ConnectionChanged;

    public FacebookConnectionMonitor(
        IFacebookService facebookService,
        ILogger<FacebookConnectionMonitor> logger)
    {
        _facebookService = facebookService;
        _logger = logger;

        _facebookService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public string GetAuthorizationUrl()
    {
        _logger.LogWarning(
            "Facebook authorization still requires a manually provisioned Page access token. See docs\\help\\connecting-to-facebook.md.");
        return string.Empty;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Start(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Stop(cancellationToken);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Facebook connection monitor");
        await RefreshConnectionState(cancellationToken);

        if (_facebookService.ConnectionState == FacebookConnectionState.Authorized)
        {
            try
            {
                await _facebookService.Connect(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to auto-connect Facebook on monitor start");
            }
        }
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping Facebook connection monitor");
        if (_facebookService.Connected)
        {
            await _facebookService.Disconnect(cancellationToken);
        }
    }

    public async Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        await _facebookService.RefreshConnectionState(cancellationToken);
        await _facebookService.RefreshStreamState(cancellationToken);

        var wasConnected = IsConnected;
        IsConnected = _facebookService.Connected;

        if (wasConnected != IsConnected)
        {
            _logger.LogInformation(
                "Facebook connection state changed to {State}",
                IsConnected ? "connected" : "disconnected");

            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnConnectionStateChanged(object? sender, FacebookConnectionState state)
    {
        _ = RefreshConnectionState();
    }
}
