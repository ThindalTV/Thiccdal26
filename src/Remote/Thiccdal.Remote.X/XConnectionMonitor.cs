using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.X;

namespace Thiccdal.Remote.X;

public class XConnectionMonitor : IXConnectionMonitor, IIntegrationConnectionMonitor, IHostedService
{
    private readonly IXService _xService;
    private readonly XOptions _options;
    private readonly ILogger<XConnectionMonitor> _logger;

    public string PlatformName => "X";
    public bool IsConnected { get; private set; }

    public event EventHandler? ConnectionChanged;

    public XConnectionMonitor(
        IXService xService,
        IOptions<XOptions> options,
        ILogger<XConnectionMonitor> logger)
    {
        _xService = xService;
        _options = options.Value;
        _logger = logger;

        _xService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public string GetAuthorizationUrl()
    {
        _logger.LogInformation("X uses developer-portal token provisioning; returning the operator setup URL");
        return _options.AuthorizationUrl;
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
        _logger.LogInformation("Starting X connection monitor");
        await RefreshConnectionState(cancellationToken);

        if (_xService.ConnectionState == XConnectionState.Authorized)
        {
            try
            {
                await _xService.Connect(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to auto-connect X on monitor start");
            }
        }
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping X connection monitor");
        if (_xService.Connected)
        {
            await _xService.Disconnect(cancellationToken);
        }
    }

    public async Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        await _xService.RefreshConnectionState(cancellationToken);
        await _xService.RefreshStreamState(cancellationToken);

        var wasConnected = IsConnected;
        IsConnected = _xService.Connected;

        if (wasConnected != IsConnected)
        {
            _logger.LogInformation(
                "X connection state changed to {State}",
                IsConnected ? "connected" : "disconnected");

            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnConnectionStateChanged(object? sender, XConnectionState state)
    {
        _ = RefreshConnectionState();
    }
}
