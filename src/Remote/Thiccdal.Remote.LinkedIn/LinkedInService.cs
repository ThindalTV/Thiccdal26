using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.LinkedIn;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Remote.LinkedIn;

/// <summary>
/// LinkedIn Live integration adapter (disabled until API approval).
/// LinkedIn requires explicit API approval to enable this integration.
/// All operations log at Information level to provide visibility into what would happen if LinkedIn were approved.
/// </summary>
public sealed class LinkedInService : IPlatformConnection, IIntegrationConnectionMonitor, IRtmpRelayDestinationProvider
{
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInService> _logger;

    public LinkedInService(
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool Connected => State == PlatformConnectionState.Connected;

    public string PlatformName => "LinkedIn";

    public PlatformConnectionState State => !_options.IsEnabled
        ? PlatformConnectionState.PendingApproval
        : string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.OrganizationId)
            ? PlatformConnectionState.Disconnected
            : PlatformConnectionState.Connected;

    public string? LastError => null;

    public bool IsConnected => _options.IsEnabled;

    public event EventHandler<ChatEvent>? OnChatMessageRecieved
    {
        add { }
        remove { }
    }

    public event EventHandler<PlatformEvent>? OnPlatformEventReceived
    {
        add { }
        remove { }
    }

    public event EventHandler? ConnectionChanged
    {
        add { _ = value; }
        remove { _ = value; }
    }

    public Task Connect(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsEnabled)
        {
            _logger.LogInformation(
                "LinkedIn Live connection skipped: integration is not enabled. Awaiting API approval and configuration.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            _logger.LogWarning("LinkedIn Live connection cannot proceed: AccessToken is not configured");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Connecting to LinkedIn Live with organization {OrganizationId}", _options.OrganizationId);
        return Task.CompletedTask;
    }

    public Task Disconnect(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Disconnecting from LinkedIn Live");
        return Task.CompletedTask;
    }

    public string GetAuthorizationUrl()
    {
        _logger.LogInformation("LinkedIn Live authorization URL requested (not yet available)");
        return string.Empty;
    }

    public Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsEnabled)
        {
            _logger.LogDebug("LinkedIn Live is not enabled; connection state remains disconnected");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Refreshing LinkedIn Live connection state. IsEnabled: {IsEnabled}, HasAccessToken: {HasAccessToken}",
            _options.IsEnabled,
            !string.IsNullOrWhiteSpace(_options.AccessToken));

        return Task.CompletedTask;
    }

    public Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.CompletedTask;
        }

        if (!_options.IsEnabled)
        {
            _logger.LogDebug("LinkedIn Live message not sent: integration is not enabled: {Message}", message);
            return Task.CompletedTask;
        }

        throw new NotSupportedException("LinkedIn Live chat reposting is not supported until LinkedIn API approval is granted.");
    }

    public Task<RtmpRelayDestination?> GetRelayDestination(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.RtmpIngestUrl) || string.IsNullOrWhiteSpace(_options.StreamKey))
        {
            return Task.FromResult<RtmpRelayDestination?>(null);
        }

        return Task.FromResult<RtmpRelayDestination?>(
            new RtmpRelayDestination
            {
                PlatformName = PlatformName,
                DestinationUrl = $"{_options.RtmpIngestUrl.TrimEnd('/')}/{_options.StreamKey.Trim()}"
            });
    }
}
