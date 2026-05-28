namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Persists YouTube OAuth tokens without coupling remote adapters to a specific data store.
/// </summary>
public interface IYouTubeTokenStore
{
    /// <summary>
    /// Gets the newest stored token, if any.
    /// </summary>
    Task<YouTubeStoredToken?> GetLatestToken(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces any existing token with the supplied token.
    /// </summary>
    Task ReplaceToken(YouTubeStoredToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all stored YouTube tokens.
    /// </summary>
    Task DeleteTokens(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether any token remains valid at the supplied time.
    /// </summary>
    Task<bool> HasValidToken(DateTime utcNow, CancellationToken cancellationToken = default);
}
