using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.LinkedIn;

namespace Thiccdal.Remote.LinkedIn;

/// <summary>
/// Tracks LinkedIn Live connection state. LinkedIn Live requires explicit API approval from LinkedIn.
/// </summary>
public sealed class LinkedInConnectionMonitor : ILinkedInConnectionMonitor
{
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInConnectionMonitor> _logger;

    public string PlatformName => "LinkedIn";

    public bool IsConnected => _options.IsEnabled && !string.IsNullOrWhiteSpace(_options.AccessToken);

    public event EventHandler? ConnectionChanged
    {
        add { _ = value; }
        remove { _ = value; }
    }

    public LinkedInConnectionMonitor(
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInConnectionMonitor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GetAuthorizationUrl()
    {
        _logger.LogInformation("LinkedIn Live authorization URL requested (requires API approval first)");
        return string.Empty;
    }

    public Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsEnabled)
        {
            _logger.LogDebug("LinkedIn Live is disabled; awaiting API approval from LinkedIn");
            return Task.CompletedTask;
        }

        var hasValidConfig = !string.IsNullOrWhiteSpace(_options.AccessToken) &&
                            !string.IsNullOrWhiteSpace(_options.OrganizationId);

        if (!hasValidConfig)
        {
            _logger.LogWarning(
                "LinkedIn Live is enabled but not fully configured. AccessToken: {HasAccessToken}, OrganizationId: {HasOrgId}",
                !string.IsNullOrWhiteSpace(_options.AccessToken),
                !string.IsNullOrWhiteSpace(_options.OrganizationId));
        }

        return Task.CompletedTask;
    }
}
