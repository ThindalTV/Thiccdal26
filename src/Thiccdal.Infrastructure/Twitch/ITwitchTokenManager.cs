namespace Thiccdal.Infrastructure.Twitch;

public interface ITwitchTokenManager
{
    /// <summary>
    /// Returns the current access token, refreshing if expired.
    /// Returns <see langword="null"/> when no token has been stored yet.
    /// </summary>
    Task<string?> GetToken(CancellationToken cancellationToken = default);

    /// <summary>Returns true if a stored token exists; never throws.</summary>
    Task<bool> HasToken(CancellationToken cancellationToken = default);

    /// <summary>Forces a refresh of the stored token using its refresh token.</summary>
    Task RefreshToken(CancellationToken cancellationToken = default);

    /// <summary>Exchanges an OAuth authorization code for tokens and persists them.</summary>
    Task StoreToken(string code, CancellationToken cancellationToken = default);

    /// <summary>Deletes all stored tokens, effectively disconnecting the account.</summary>
    Task Revoke(CancellationToken cancellationToken = default);

    /// <summary>Builds the Twitch OAuth authorization URL the operator must visit.</summary>
    string GetAuthorizationUrl();

    /// <summary>
    /// Validates a one-time state token that was embedded in <see cref="GetAuthorizationUrl"/>.
    /// Returns false if the state is unknown, already consumed, or expired (replay/CSRF guard).
    /// </summary>
    bool ValidateAndConsumeState(string state);
}
