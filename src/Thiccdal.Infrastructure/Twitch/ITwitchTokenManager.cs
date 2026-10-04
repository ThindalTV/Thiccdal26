namespace Thiccdal.Infrastructure.Twitch;

public interface ITwitchTokenManager
{
    /// <summary>
    /// Returns the access token for the given account, refreshing if expired.
    /// Returns <see langword="null"/> when that account has not been authorized yet.
    /// </summary>
    Task<string?> GetToken(TwitchTokenRole role, CancellationToken cancellationToken = default);

    /// <summary>Returns true if the given account has been authorized; never throws.</summary>
    Task<bool> HasToken(TwitchTokenRole role, CancellationToken cancellationToken = default);

    /// <summary>Returns the Twitch identity behind a stored authorization, or null when there is none.</summary>
    Task<TwitchUser?> GetIdentity(TwitchTokenRole role, CancellationToken cancellationToken = default);

    /// <summary>Forces a refresh of the stored token for the given account.</summary>
    Task RefreshToken(TwitchTokenRole role, CancellationToken cancellationToken = default);

    /// <summary>Exchanges an OAuth authorization code for tokens and persists them for the given account.</summary>
    Task StoreToken(string code, TwitchTokenRole role, CancellationToken cancellationToken = default);

    /// <summary>Deletes the stored token for the given account, disconnecting it.</summary>
    Task Revoke(TwitchTokenRole role, CancellationToken cancellationToken = default);

    /// <summary>Builds the Twitch OAuth authorization URL for the given account, with the scopes that account can grant.</summary>
    string GetAuthorizationUrl(TwitchTokenRole role);

    /// <summary>
    /// Validates a one-time state token that was embedded in <see cref="GetAuthorizationUrl"/> and
    /// reports which account the authorization was started for.
    /// Returns false if the state is unknown, already consumed, or expired (replay/CSRF guard).
    /// </summary>
    bool ValidateAndConsumeState(string state, out TwitchTokenRole role);
}
