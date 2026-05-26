namespace Thiccdal.Infrastructure.Integrations;

/// <summary>
/// Tracks connection state for a single platform integration and notifies observers when it changes.
/// Register one implementation per platform; inject <see cref="IEnumerable{T}"/> to enumerate all.
/// </summary>
public interface IIntegrationConnectionMonitor
{
    /// <summary>Gets the display name of the platform (e.g. "Twitch").</summary>
    string PlatformName { get; }

    /// <summary>Gets a value indicating whether the platform currently has a valid authenticated session.</summary>
    bool IsConnected { get; }

    /// <summary>Returns the OAuth authorization URL used to initiate the connection flow.</summary>
    string GetAuthorizationUrl();

    /// <summary>Raised when <see cref="IsConnected"/> changes.</summary>
    event EventHandler? ConnectionChanged;

    /// <summary>
    /// Re-checks the underlying token store and updates <see cref="IsConnected"/>,
    /// raising <see cref="ConnectionChanged"/> if the state changed.
    /// </summary>
    Task RefreshConnectionState(CancellationToken cancellationToken = default);
}
