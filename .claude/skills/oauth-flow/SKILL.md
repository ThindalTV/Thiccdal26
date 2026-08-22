---
name: oauth-flow
description: OAuth 2.0 authorization-code flow rules for this repo — the mandatory CSRF state parameter, callback endpoint shape, token revocation on disconnect, and the tests every platform must have. Use when implementing or changing OAuth for any platform integration.
---

# OAuth authorization-code flow

Applies to every platform integration that authorises a real account: Twitch, YouTube, Discord,
Facebook, X, LinkedIn, TikTok.

## The `state` parameter is not optional

Without it, an attacker can inject an authorization code from their own OAuth flow into the
victim's callback URL, and the server stores attacker-controlled credentials against the victim's
account. That is OAuth CSRF, and it means someone else's channel token in your database.

State lives in the singleton token manager, because a Blazor Server app has many circuits hitting
the same service:

```csharp
// ConcurrentDictionary — singleton reached from multiple circuits
private readonly ConcurrentDictionary<string, DateTime> _pendingStates = new();

public string GetAuthorizationUrl()
{
    DateTime now = DateTime.UtcNow;

    // Best-effort prune; no background timer needed
    foreach ((string key, DateTime expiry) in _pendingStates)
    {
        if (expiry < now)
        {
            _pendingStates.TryRemove(key, out _);
        }
    }

    // 256 bits of entropy, URL-safe Base64
    string state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    _pendingStates[state] = now.AddMinutes(10);

    return $"{_options.AuthorizeEndpoint}?client_id={_options.ClientId}"
        + $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}"
        + $"&response_type=code"
        + $"&scope={Uri.EscapeDataString(RequiredScopes)}"
        + $"&state={Uri.EscapeDataString(state)}";
}

public bool ValidateAndConsumeState(string state)
{
    if (_pendingStates.TryRemove(state, out DateTime expiry))
    {
        return expiry >= DateTime.UtcNow;
    }

    return false;
}
```

Key properties: **one-time use** (`TryRemove` consumes on first valid check, so replay fails),
**10-minute TTL** (these are operator-driven flows, not automated), **thread-safe**.

Existing implementations: `TwitchTokenManager`, and the YouTube equivalent wired in
`YouTubeRegistrationExtensions`.

## Callback endpoint

Handle the error case, the missing-code case, and the state case separately, and redirect with a
distinguishable reason rather than throwing:

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
    ILogger logger = loggerFactory.CreateLogger("Auth.Callback");

    if (!string.IsNullOrEmpty(error))
    {
        logger.LogWarning("OAuth error: {Error} — {Description}", error, error_description);
        return Results.Redirect("/connect?error=oauth_denied");
    }

    if (string.IsNullOrEmpty(code))
    {
        return Results.Redirect("/connect?error=missing_code");
    }

    if (string.IsNullOrEmpty(state) || !tokenManager.ValidateAndConsumeState(state))
    {
        logger.LogWarning("OAuth state validation failed — possible CSRF");
        return Results.Redirect("/connect?error=invalid_state");
    }

    await tokenManager.StoreToken(code, cancellationToken);
    return Results.Redirect("/connect");
});
```

Never log the code, the state value, or the resulting token. After storing, call
`RefreshConnectionState` on the platform's connection monitor so subscribed circuits re-render.

## Disconnect revokes remotely first

Call the platform's revocation endpoint **before** removing local storage, with a short timeout so
an unreachable platform cannot wedge the operator:

```csharp
using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeout.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    await _httpClient.PostAsync(revokeEndpoint, content, timeout.Token);
}
catch (Exception ex)
{
    // Best-effort: log, then always remove locally
    _logger.LogWarning(ex, "Failed to call revocation API; proceeding with local removal");
}
```

Local removal happens either way — an operator who clicks disconnect must end up disconnected.

## Required tests per platform

1. `GetAuthorizationUrl_ContainsStateParameter`
2. `GetAuthorizationUrl_EachCallProducesUniqueState`
3. `WhenStateWasIssued_ValidateAndConsumeStateReturnsTrue`
4. `WhenStateNeverIssued_ValidateAndConsumeStateReturnsFalse`
5. `WhenStateConsumedTwice_SecondCallReturnsFalse` — replay protection

Use fake credentials in fixtures. Never a redacted real one.
