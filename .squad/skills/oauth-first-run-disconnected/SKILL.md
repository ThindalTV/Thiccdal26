---
name: "oauth-first-run-disconnected"
description: "Model first-run OAuth absence as an explicit disconnected state instead of an exception."
domain: "integrations"
confidence: "high"
source: "earned"
---

## Context

Use when a platform adapter can start before the operator has completed OAuth at least once, but background services still probe auth state or attempt connections during app boot.

## Pattern

1. Keep the token-manager API explicit about three outcomes:
   - token available
   - token missing because the app has never been authorized
   - real failure (storage/refresh/network)
2. Represent "never authorized" with a non-exceptional return such as `null`, not a thrown `InvalidOperationException`.
3. In startup-facing services, translate the missing-token result into the platform's disconnected/not-authorized state and skip remote calls.
4. Continue throwing for real token refresh or transport failures so operators still see broken integrations promptly.

## Twitch example

- `src\Thiccdal.Infrastructure\Twitch\ITwitchTokenManager.cs`
- `src\Remote\Thiccdal.Remote.Twitch\TwitchTokenManager.cs`
- `src\Remote\Thiccdal.Remote.Twitch\TwitchService.cs`
- `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchTokenManagerTests.cs`

## Anti-patterns

- Throwing "no token found" from the token manager during first startup.
- Swallowing actual refresh/API/storage failures under the same no-token path.
- Attempting IRC/HTTP connections when authorization is still missing.
