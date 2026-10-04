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

    // Pending OAuth state tokens: value carries the expiry and the account the flow was started for.
    // Concurrent because the singleton may be accessed from multiple circuits.
    private readonly ConcurrentDictionary<string, PendingAuthorization> _pendingStates = new();

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

    public async Task<string?> GetToken(TwitchTokenRole role, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var storedToken = await FindToken(context, role, cancellationToken);

        if (storedToken == null)
        {
            _logger.LogInformation("No Twitch {Role} token found; treating that account as not authorized", role);
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

    public async Task<bool> HasToken(TwitchTokenRole role, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await context.TwitchTokens.AnyAsync(token => token.Role == role, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to check token existence for the Twitch {Role} account", role);
            return false;
        }
    }

    public async Task<TwitchUser?> GetIdentity(TwitchTokenRole role, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var storedToken = await FindToken(context, role, cancellationToken);

            if (storedToken == null || string.IsNullOrWhiteSpace(storedToken.UserId))
            {
                return null;
            }

            return new TwitchUser
            {
                Id = storedToken.UserId,
                Login = storedToken.Username,
                DisplayName = storedToken.Username
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Unable to read the stored Twitch identity for the {Role} account", role);
            return null;
        }
    }

    public async Task RefreshToken(TwitchTokenRole role, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var storedToken = await FindToken(context, role, cancellationToken);

        if (storedToken != null)
        {
            await RefreshStoredToken(context, storedToken, cancellationToken);
        }
    }

    public async Task StoreToken(string code, TwitchTokenRole role, CancellationToken cancellationToken = default)
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
            Role = role,
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300)
        };

        await PopulateUserInfo(token, cancellationToken);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Replace the existing token for this account — one valid token per role at a time.
        var existing = await context.TwitchTokens.Where(stored => stored.Role == role).ToListAsync(cancellationToken);
        context.TwitchTokens.RemoveRange(existing);
        context.TwitchTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Twitch {Role} token stored successfully for user {Username} (ID: {UserId})",
            role,
            token.Username,
            token.UserId);
    }

    public async Task Revoke(TwitchTokenRole role, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Revoking stored Twitch tokens for the {Role} account", role);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var tokens = await context.TwitchTokens.Where(token => token.Role == role).ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            _logger.LogDebug("No Twitch {Role} tokens to revoke", role);
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

        _logger.LogInformation("Revoked {Count} Twitch {Role} token(s)", tokens.Count, role);
    }

    public string GetAuthorizationUrl(TwitchTokenRole role)
    {
        var now = DateTime.UtcNow;

        // Prune expired states to prevent unbounded growth.
        foreach (var (key, pending) in _pendingStates)
            if (pending.ExpiresAt < now) _pendingStates.TryRemove(key, out _);

        // URL-safe Base64 state token (256 bits of entropy).
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _pendingStates[state] = new PendingAuthorization(now.AddMinutes(10), role);

        string authorizeUrl = BuildOAuthEndpointUri("authorize").ToString();

        return $"{authorizeUrl}" +
               $"?client_id={_options.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
               $"&response_type=code" +
               $"&scope={Uri.EscapeDataString(BuildRequiredScopes(role))}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public bool ValidateAndConsumeState(string state, out TwitchTokenRole role)
    {
        role = TwitchTokenRole.Bot;

        if (!_pendingStates.TryRemove(state, out var pending))
        {
            return false;
        }

        role = pending.Role;
        return pending.ExpiresAt >= DateTime.UtcNow;
    }

    private static Task<TwitchToken?> FindToken(
        ApplicationDbContext context,
        TwitchTokenRole role,
        CancellationToken cancellationToken)
    {
        return context.TwitchTokens
            .Where(token => token.Role == role)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
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

    private string BuildRequiredScopes(TwitchTokenRole role)
    {
        var configuredScopes = role == TwitchTokenRole.Broadcaster
            ? _options.BroadcasterScopes
            : _options.BotScopes;

        var scopes = configuredScopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Follower reads are a moderator permission, and the broadcaster moderates their own channel.
        if (role == TwitchTokenRole.Broadcaster &&
            _options.EventSub.RequireModeratorAccess &&
            !scopes.Any(scope => string.Equals(scope, "moderator:read:followers", StringComparison.Ordinal)))
        {
            scopes.Add("moderator:read:followers");
        }

        return string.Join(' ', scopes);
    }

    private sealed record PendingAuthorization(DateTime ExpiresAt, TwitchTokenRole Role);

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