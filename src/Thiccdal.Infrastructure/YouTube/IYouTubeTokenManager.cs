namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Manages OAuth token lifecycle for YouTube integration.
/// </summary>
public interface IYouTubeTokenManager
{
    /// <summary>Generates the YouTube OAuth authorization URL with PKCE state parameter.</summary>
    string GetAuthorizationUrl();

    /// <summary>Validates and consumes a PKCE state token to prevent CSRF attacks.</summary>
    bool ValidateAndConsumeState(string state);

    /// <summary>Exchanges an authorization code for an access token and stores it.</summary>
    Task StoreToken(string authorizationCode, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the current access token, refreshing if necessary.</summary>
    Task<string?> GetToken(CancellationToken cancellationToken = default);

    /// <summary>Checks if a valid token exists without retrieving it.</summary>
    Task<bool> HasToken(CancellationToken cancellationToken = default);

    /// <summary>Revokes and deletes the stored token.</summary>
    Task RevokeToken(CancellationToken cancellationToken = default);
}
