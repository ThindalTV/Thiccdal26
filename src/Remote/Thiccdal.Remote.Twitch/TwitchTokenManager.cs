using System;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Data;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public class TwitchTokenManager : ITwitchTokenManager
{
    private readonly TwitchOptions _options;
    private readonly ILogger<TwitchTokenManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;

    public TwitchTokenManager(
        IOptions<TwitchOptions> options,
        ILogger<TwitchTokenManager> logger,
        IHttpClientFactory httpClientFactory,
        ApplicationDbContext context)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Twitch");
        _context = context;
    }

    public async Task<string> GetToken(CancellationToken cancellationToken = default)
    {
        var storedToken = await _context.TwitchTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedToken == null)
        {
            throw new InvalidOperationException("No Twitch token found. Please authorize the application first.");
        }

        if (DateTime.UtcNow < storedToken.ExpiresAt)
        {
            _logger.LogDebug("Using valid stored Twitch token");
            return storedToken.AccessToken;
        }

        _logger.LogInformation("Token expired, refreshing");
        return await RefreshStoredToken(storedToken, cancellationToken);
    }

    public async Task RefreshToken(CancellationToken cancellationToken = default)
    {
        var storedToken = await _context.TwitchTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedToken != null)
        {
            await RefreshStoredToken(storedToken, cancellationToken);
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

        var response = await _httpClient.PostAsync(
            "https://id.twitch.tv/oauth2/token",
            new FormUrlEncodedContent(requestData),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitch token exchange failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new InvalidOperationException($"Twitch token exchange failed: {errorContent}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        
        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Failed to deserialize token response");
        }

        var token = new TwitchToken
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300)
        };

        _context.TwitchTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Twitch token stored successfully");
    }

    public string GetAuthorizationUrl()
    {
        var scopes = "chat:read chat:edit";
        return $"https://id.twitch.tv/oauth2/authorize?client_id={_options.ClientId}&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}&response_type=code&scope={Uri.EscapeDataString(scopes)}";
    }

    private async Task<string> RefreshStoredToken(TwitchToken token, CancellationToken cancellationToken)
    {
        var requestData = new Dictionary<string, string>
        {
            { "client_id", _options.ClientId },
            { "client_secret", _options.ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", token.RefreshToken }
        };

        var response = await _httpClient.PostAsync(
            "https://id.twitch.tv/oauth2/token",
            new FormUrlEncodedContent(requestData),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitch token refresh failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new InvalidOperationException($"Twitch token refresh failed: {errorContent}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        
        if (tokenResponse == null)
        {
            throw new InvalidOperationException("Failed to refresh token");
        }

        token.AccessToken = tokenResponse.AccessToken;
        token.RefreshToken = tokenResponse.RefreshToken;
        token.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Token refreshed successfully");
        
        return token.AccessToken;
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType);
}
