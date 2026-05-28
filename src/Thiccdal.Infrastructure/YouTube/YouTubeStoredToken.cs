namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Represents a persisted YouTube OAuth token snapshot.
/// </summary>
public sealed record YouTubeStoredToken
{
    /// <summary>
    /// Gets the persisted token identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the access token used for authenticated API calls.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the refresh token used to renew access when the access token expires.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC expiration timestamp for the access token.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Gets when the token snapshot was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
