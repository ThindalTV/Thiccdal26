using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Data;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

internal sealed class TwitchTokenManager : ITwitchTokenManager
{
    private readonly TwitchOptions _options;
    private readonly ILogger<TwitchTokenManager> _logger;
    private readonly HttpClient _oauthHttpClient;
    private readonly HttpClient _helixHttpClient;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    // Pending OAuth state tokens: value is the expiry time.
    // Concurrent because the singleton may be accessed from multiple circuits.
    private readonly ConcurrentDictionary<string, DateTime> _pendingStates = new();

    public TwitchTokenManager(
        IOptions<TwitchOptions> options,
        ILogger<TwitchTokenManager> logger,
        IHttpClientFactory httpClientFactory,
        IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _options = options.Value;
        _logger = logger;
        _oauthHttpClient = httpClientFactory.CreateClient(TwitchClientNames.OAuth);
        _helixHttpClient = httpClientFactory.CreateClient(TwitchClientNames.Helix);
        _dbContextFactory = dbContextFactory;
    }

    public async Task<string?> GetToken(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var storedToken = await context.TwitchTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedToken == null)
        {
            _logger.LogInformation("No Twitch token found; treating Twitch as not authorized");
            return null;
        }

        if (DateTime.UtcNow < storedToken.ExpiresAt)
        {
            _logger.LogDebug("Using valid stored Twitch token");
            return storedToken.AccessToken;
        }

        _logger.LogInformation("Token expired, refreshing");
        return await RefreshStoredToken(context, storedToken, cancellationToken);
    }

    public async Task<bool> HasToken(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await context.TwitchTokens.AnyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to check token existence");
            return false;
        }
    }

    public async Task RefreshToken(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var storedToken = await context.TwitchTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedToken != null)
        {
            await RefreshStoredToken(context, storedToken, cancellationToken);
        }
    }

    public async Task StoreToken(string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exchanging authorization code for tokens");

        var requestData = new Dictionary<string, string>
        {
            { "client_id", _options.ClientId },
            { "client_secret", _options.ClientSecret },
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", _options.RedirectUri }
        };

        var response = await _oauthHttpClient.PostAsync(
            BuildOAuthEndpointUri("token"),
            new FormUrlEncodedContent(requestData),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitch token exchange failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new InvalidOperationException($"Twitch token exchange failed: {errorContent}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize token response");

        var token = new TwitchToken
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300)
        };

        await PopulateUserInfo(token, cancellationToken);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Replace any existing tokens — only one valid token per application at a time.
        var existing = await context.TwitchTokens.ToListAsync(cancellationToken);
        context.TwitchTokens.RemoveRange(existing);
        context.TwitchTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Twitch token stored successfully for user {Username} (ID: {UserId})", token.Username, token.UserId);
    }

    public async Task Revoke(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Revoking stored Twitch tokens");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var tokens = await context.TwitchTokens.ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            _logger.LogDebug("No Twitch tokens to revoke");
            return;
        }

        // Best-effort: call Twitch revocation API for each token before removing locally.
        // Revoking the access token also invalidates the associated refresh token server-side.
        foreach (var token in tokens)
        {
            try
            {
                var revokeData = new Dictionary<string, string>
                {
                    { "client_id", _options.ClientId },
                    { "token", token.AccessToken }
                };

                using var revokeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                revokeTimeout.CancelAfter(TimeSpan.FromSeconds(5));

                var response = await _oauthHttpClient.PostAsync(
                    BuildOAuthEndpointUri("revoke"),
                    new FormUrlEncodedContent(revokeData),
                    revokeTimeout.Token);

                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("Twitch revocation API returned {StatusCode}; continuing with local removal", response.StatusCode);
            }
            catch (Exception ex)
            {
                // Always remove locally even if the Twitch API is unreachable — operator must not be left in a stuck state.
                _logger.LogWarning(ex, "Failed to call Twitch revocation API; proceeding with local removal");
            }
        }

        context.TwitchTokens.RemoveRange(tokens);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Revoked {Count} Twitch token(s)", tokens.Count);
    }

    public string GetAuthorizationUrl()
    {
        var now = DateTime.UtcNow;

        // Prune expired states to prevent unbounded growth.
        foreach (var (key, expiry) in _pendingStates)
            if (expiry < now) _pendingStates.TryRemove(key, out _);

        // URL-safe Base64 state token (256 bits of entropy).
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _pendingStates[state] = now.AddMinutes(10);

        string authorizeUrl = BuildOAuthEndpointUri("authorize").ToString();

        return $"{authorizeUrl}" +
               $"?client_id={_options.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
               $"&response_type=code" +
               $"&scope={Uri.EscapeDataString(BuildRequiredScopes())}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public bool ValidateAndConsumeState(string state)
    {
        if (_pendingStates.TryRemove(state, out var expiry))
            return expiry >= DateTime.UtcNow;

        return false;
    }

    private async Task<string> RefreshStoredToken(ApplicationDbContext context, TwitchToken token, CancellationToken cancellationToken)
    {
        var requestData = new Dictionary<string, string>
        {
            { "client_id", _options.ClientId },
            { "client_secret", _options.ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", token.RefreshToken }
        };

        var response = await _oauthHttpClient.PostAsync(
            BuildOAuthEndpointUri("token"),
            new FormUrlEncodedContent(requestData),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitch token refresh failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new InvalidOperationException($"Twitch token refresh failed: {errorContent}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to refresh token");

        token.AccessToken = tokenResponse.AccessToken;
        token.RefreshToken = tokenResponse.RefreshToken;
        token.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300);

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Token refreshed successfully");

        return token.AccessToken;
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType);

    private Uri BuildOAuthEndpointUri(string relativePath)
    {
        string oauthBaseAddress = _options.OAuthBaseAddress.EndsWith('/')
            ? _options.OAuthBaseAddress
            : $"{_options.OAuthBaseAddress}/";

        return new Uri(new Uri(oauthBaseAddress, UriKind.Absolute), relativePath);
    }

    private string BuildRequiredScopes()
    {
        var scopes = _options.Scopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (_options.EventSub.RequireModeratorAccess &&
            !scopes.Any(scope => string.Equals(scope, "moderator:read:followers", StringComparison.Ordinal)))
        {
            scopes.Add("moderator:read:followers");
        }

        return string.Join(' ', scopes);
    }

    private async Task PopulateUserInfo(TwitchToken token, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Fetching authenticated user info from Twitch Helix API");
            
            using var request = new HttpRequestMessage(HttpMethod.Get, "users");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
            request.Headers.Add("Client-Id", _options.ClientId);

            using HttpResponseMessage response = await _helixHttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<HelixUsersResponse>(cancellationToken: cancellationToken);
            var userData = payload?.Data?.FirstOrDefault();
            
            if (userData != null)
            {
                token.Username = userData.Login;
                token.UserId = userData.Id;
                _logger.LogInformation("Retrieved user info: {Username} (ID: {UserId})", userData.Login, userData.Id);
            }
            else
            {
                _logger.LogWarning("Unable to fetch authenticated user info; token will not have username/user ID populated");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch authenticated user info; token will not have username/user ID populated");
        }
    }

    private sealed record HelixUsersResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<HelixUserData>? Data);

    private sealed record HelixUserData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("display_name")] string DisplayName);
}