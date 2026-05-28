using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Remote.Null;

public sealed class NullPlatformConnection : IPlatformConnection, IIntegrationConnectionMonitor, IRtmpRelayDestinationProvider
{
    private readonly NullOptions _options;
    private readonly ILogger<NullPlatformConnection> _logger;

    private bool _connected;

    public NullPlatformConnection(
        IOptions<NullOptions> options,
        ILogger<NullPlatformConnection> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool Connected => _connected;

    public bool IsConnected => _connected;

    public string PlatformName => _options.PlatformName;

    public PlatformConnectionState State => _connected
        ? PlatformConnectionState.Connected
        : PlatformConnectionState.Disconnected;

    public string? LastError => null;

    public event EventHandler<ChatEvent>? OnChatMessageRecieved;

    public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

    public event EventHandler? ConnectionChanged;

    public Task Connect(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Connecting null platform {PlatformName}", PlatformName);
        return SetConnectionState(true);
    }

    public Task Disconnect(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Disconnecting null platform {PlatformName}", PlatformName);
        return SetConnectionState(false);
    }

    public string GetAuthorizationUrl()
    {
        _logger.LogInformation("Resolving authorization URL for null platform {PlatformName}", PlatformName);
        return _options.AuthorizationUrl;
    }

    public Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Refreshing null platform {PlatformName} connection state. Connected: {Connected}",
            PlatformName,
            Connected);

        return Task.CompletedTask;
    }

    public Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        return SendMessage(message, channelId: null, cancellationToken);
    }

    public Task SendMessage(string message, string? channelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Discarding outbound null platform message for {PlatformName}/{ChannelId}: {Message}",
            PlatformName,
            string.IsNullOrWhiteSpace(channelId) ? "(default)" : channelId,
            message);
        return Task.CompletedTask;
    }

    public Task PublishEvent(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(platformEvent);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Publishing null platform event for {PlatformName}: {EventType}/{ExternalId}",
            PlatformName,
            platformEvent.Type,
            platformEvent.ExternalId);

        OnPlatformEventReceived?.Invoke(this, platformEvent);

        if (platformEvent is ChatEvent chatEvent)
        {
            OnChatMessageRecieved?.Invoke(this, chatEvent);
        }

        return Task.CompletedTask;
    }

    public Task<RtmpRelayDestination?> GetRelayDestination(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.RtmpRelayUrl))
        {
            return Task.FromResult<RtmpRelayDestination?>(null);
        }

        return Task.FromResult<RtmpRelayDestination?>(
            new RtmpRelayDestination
            {
                PlatformName = PlatformName,
                DestinationUrl = _options.RtmpRelayUrl.Trim()
            });
    }

    private Task SetConnectionState(bool connected)
    {
        if (_connected == connected)
        {
            return Task.CompletedTask;
        }

        _connected = connected;
        ConnectionChanged?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }
}
