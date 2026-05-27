# SKILL: Typed Platform HTTP Client Seam

## When to Apply

Use when an integration is migrating off an older transport or ad-hoc HTTP calls, but only part of the adapter can move in the current slice.

## Pattern

1. Define a narrow infrastructure interface for the external REST boundary (for example `ITwitchHelixClient`).
2. Keep platform service orchestration in the adapter service, but move request construction, auth headers, and response DTO parsing into the typed client.
3. Replace one meaningful legacy-dependent path at a time (for example stream lookup or outbound chat) instead of attempting the full transport rewrite in one change.
4. Keep a temporary fallback to the legacy path when that preserves current app behavior during the migration.
5. Cover both layers separately:
   - client tests for HTTP method, route, headers, and payload mapping
   - service tests for choosing the typed seam and passing the resolved connection profile

## Why

This keeps the migration incremental and testable. It also prevents the adapter service from turning into a mixed transport bucket while the rewrite is in flight.

## Twitch Example

- `src\Thiccdal.Infrastructure\Twitch\ITwitchHelixClient.cs`
- `src\Remote\Thiccdal.Remote.Twitch\TwitchHelixClient.cs`
- `src\Remote\Thiccdal.Remote.Twitch\TwitchService.cs`
- `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchHelixClientTests.cs`
