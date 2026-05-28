using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.Data;

/// <summary>
/// Persists YouTube OAuth tokens in the application database.
/// </summary>
public sealed class YouTubeTokenStore : IYouTubeTokenStore
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public YouTubeTokenStore(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<YouTubeStoredToken?> GetLatestToken(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        YouTubeToken? token = await context.YouTubeTokens
            .OrderByDescending(static token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return token is null
            ? null
            : new YouTubeStoredToken
            {
                Id = token.Id,
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresAt = token.ExpiresAt,
                CreatedAt = token.CreatedAt
            };
    }

    public async Task ReplaceToken(YouTubeStoredToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM YouTubeTokens", cancellationToken);
        context.YouTubeTokens.Add(new YouTubeToken
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = token.ExpiresAt,
            CreatedAt = token.CreatedAt
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTokens(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM YouTubeTokens", cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasValidToken(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.YouTubeTokens.AnyAsync(token => token.ExpiresAt > utcNow, cancellationToken);
    }
}
