using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.Remote.YouTube;

public sealed class YouTubeTokenManager : IYouTubeTokenManager
{
    private readonly YouTubeOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IYouTubeTokenStore _tokenStore;
    private readonly ILogger<YouTubeTokenManager> _logger;
    private readonly HashSet<string> _pendingStates = new();
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    public YouTubeTokenManager(
        IOptions<YouTubeOptions> options,
        IHttpClientFactory httpClientFactory,
        IYouTubeTokenStore tokenStore,
        ILogger<YouTubeTokenManager> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public string GetAuthorizationUrl()
    {
        string state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _stateLock.Wait();
        try
        {
            _pendingStates.Add(state);
        }
        finally
        {
            _stateLock.Release();
        }

        string scopes = string.Join(" ", _options.Scopes);
        string authUrl = $"{_options.OAuthBaseAddress}auth?" +
                        $"client_id={Uri.EscapeDataString(_options.ClientId)}&" +
                        $"redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}&" +
                        $"response_type=code&" +
                        $"scope={Uri.EscapeDataString(scopes)}&" +
                        $"state={Uri.EscapeDataString(state)}&" +
                        $"access_type=offline&" +
                        $"prompt=consent";

        return authUrl;
    }

    public bool ValidateAndConsumeState(string state)
    {
        _stateLock.Wait();
        try
        {
            return _pendingStates.Remove(state);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StoreToken(string authorizationCode, CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient(YouTubeClientNames.OAuth);

        var requestBody = new Dictionary<string, string>
        {
            { "code", authorizationCode },
            { "client_id", _options.ClientId },
            { "client_secret", _options.ClientSecret },
            { "redirect_uri", _options.RedirectUri },
            { "grant_type", "authorization_code" }
        };

        var response = await httpClient.PostAsync(
            "token",
            new FormUrlEncodedContent(requestBody),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string accessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
        string? refreshToken = root.TryGetProperty("refresh_token", out var refreshProp)
            ? refreshProp.GetString()
            : null;
        int expiresIn = root.GetProperty("expires_in").GetInt32();

        var tokenRecord = new YouTubeStoredToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            CreatedAt = DateTime.UtcNow
        };

        await _tokenStore.ReplaceToken(tokenRecord, cancellationToken);

        _logger.LogInformation("Stored YouTube OAuth token (expires {ExpiresAt:u})", tokenRecord.ExpiresAt);
    }

    public async Task<string?> GetToken(CancellationToken cancellationToken = default)
    {
        YouTubeStoredToken? tokenRecord = await _tokenStore.GetLatestToken(cancellationToken);

        if (tokenRecord is null)
        {
            return null;
        }

        if (tokenRecord.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            return tokenRecord.AccessToken;
        }

        if (string.IsNullOrWhiteSpace(tokenRecord.RefreshToken))
        {
            _logger.LogWarning("YouTube token expired and no refresh token available");
            return null;
        }

        return await RefreshAccessToken(tokenRecord, cancellationToken);
    }

    public async Task<bool> HasToken(CancellationToken cancellationToken = default)
    {
        return await _tokenStore.HasValidToken(DateTime.UtcNow, cancellationToken);
    }

    public async Task RevokeToken(CancellationToken cancellationToken = default)
    {
        YouTubeStoredToken? tokenRecord = await _tokenStore.GetLatestToken(cancellationToken);

        if (tokenRecord is not null)
        {
            var httpClient = _httpClientFactory.CreateClient(YouTubeClientNames.OAuth);
            try
            {
                await httpClient.PostAsync(
                    $"revoke?token={Uri.EscapeDataString(tokenRecord.AccessToken)}",
                    null,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to revoke YouTube token at Google");
            }

            await _tokenStore.DeleteTokens(cancellationToken);
        }

        _logger.LogInformation("Revoked YouTube token");
    }

    private async Task<string?> RefreshAccessToken(YouTubeStoredToken tokenRecord, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(YouTubeClientNames.OAuth);

        var requestBody = new Dictionary<string, string>
        {
            { "client_id", _options.ClientId },
            { "client_secret", _options.ClientSecret },
            { "refresh_token", tokenRecord.RefreshToken },
            { "grant_type", "refresh_token" }
        };

        var response = await httpClient.PostAsync(
            "token",
            new FormUrlEncodedContent(requestBody),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string newAccessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
        int expiresIn = root.GetProperty("expires_in").GetInt32();

        YouTubeStoredToken updatedToken = tokenRecord with
        {
            AccessToken = newAccessToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
        };

        await _tokenStore.ReplaceToken(updatedToken, cancellationToken);

        _logger.LogInformation("Refreshed YouTube access token (expires {ExpiresAt:u})", updatedToken.ExpiresAt);
        return newAccessToken;
    }
}
