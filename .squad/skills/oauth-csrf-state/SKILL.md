# SKILL: OAuth CSRF State Parameter Pattern

## When to Apply

Any time an OAuth 2.0 authorization code flow is implemented. Applies to all platform integrations (Twitch, YouTube, Kick, Discord, etc.).

## The Risk

Without a `state` parameter, an attacker can inject a valid authorization code (from their own OAuth flow) into the victim's callback URL, causing the server to store attacker-controlled credentials. This is OAuth CSRF.

## Pattern (C# / Singleton Service)

### Interface addition

```csharp
/// <summary>Validates and consumes a one-time OAuth state token (CSRF guard).</summary>
bool ValidateAndConsumeState(string state);
```

### Implementation (in singleton token manager)

```csharp
// Field — ConcurrentDictionary because singleton may be accessed from multiple Blazor circuits
private readonly ConcurrentDictionary<string, DateTime> _pendingStates = new();

public string GetAuthorizationUrl()
{
    var now = DateTime.UtcNow;

    // Prune expired states to prevent unbounded growth
    foreach (var (key, expiry) in _pendingStates)
        if (expiry < now) _pendingStates.TryRemove(key, out _);

    // 256 bits of entropy, URL-safe Base64
    var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    _pendingStates[state] = now.AddMinutes(10);

    return $"https://platform.example.com/oauth2/authorize" +
           $"?client_id={_options.ClientId}" +
           $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
           $"&response_type=code" +
           $"&scope={Uri.EscapeDataString(RequiredScopes)}" +
           $"&state={Uri.EscapeDataString(state)}";
}

public bool ValidateAndConsumeState(string state)
{
    if (_pendingStates.TryRemove(state, out var expiry))
        return expiry >= DateTime.UtcNow;
    return false;
}
```

### Callback endpoint (ASP.NET Core minimal API)

```csharp
app.MapGet("/auth/{platform}/callback", async (
    string? code,
    string? state,
    string? error,
    string? error_description,
    ITokenManager tokenManager,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("Auth.Callback");

    if (!string.IsNullOrEmpty(error))
    {
        logger.LogWarning("OAuth error: {Error} — {Description}", error, error_description);
        return Results.Redirect("/connect?error=oauth_denied");
    }

    if (string.IsNullOrEmpty(code))
    {
        logger.LogWarning("OAuth callback: no code and no error");
        return Results.Redirect("/connect?error=missing_code");
    }

    if (string.IsNullOrEmpty(state) || !tokenManager.ValidateAndConsumeState(state))
    {
        logger.LogWarning("OAuth state validation failed — possible CSRF (state={State})", state);
        return Results.Redirect("/connect?error=invalid_state");
    }

    await tokenManager.StoreToken(code, cancellationToken);
    return Results.Redirect("/connect");
});
```

## Required Usings

```csharp
using System.Collections.Concurrent;
using System.Security.Cryptography;
```

## Key Properties

- **One-time use:** `TryRemove` consumes on first valid check — replay is rejected
- **Expiry:** 10-minute TTL is practical for operator-driven flows (not automated)
- **Thread-safe:** `ConcurrentDictionary` is safe for singleton with multiple circuits
- **Best-effort pruning:** prune on each `GetAuthorizationUrl()` call; no background timer needed

## Revocation Pattern (Best-Practice)

Always call the platform's token revocation endpoint BEFORE removing from local storage. Use a short timeout so operators can't get stuck:

```csharp
using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeout.CancelAfter(TimeSpan.FromSeconds(5));
try
{
    await _httpClient.PostAsync(
        "https://platform.example.com/oauth2/revoke",
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", _options.ClientId },
            { "token", token.AccessToken }
        }),
        timeout.Token);
}
catch (Exception ex)
{
    // Best-effort: log but always remove locally
    _logger.LogWarning(ex, "Failed to call revocation API; proceeding with local removal");
}
```

## Tests to Include

1. `GetAuthorizationUrl_ContainsStateParameter` — URL contains `&state=`
2. `GetAuthorizationUrl_EachCallProducesUniqueState` — states differ per call
3. `WhenStateWasIssued_ValidateAndConsumeStateReturnsTrue`
4. `WhenStateNeverIssued_ValidateAndConsumeStateReturnsFalse`
5. `WhenStateConsumedTwice_SecondCallReturnsFalse` — replay protection
