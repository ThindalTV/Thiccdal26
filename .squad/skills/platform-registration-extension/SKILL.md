# SKILL: Platform Registration Extension

## When to Apply

Use when a platform adapter needs DI registration plus one or more minimal-API callbacks or webhooks, and the app host should stay at high-level composition only.

## Pattern

1. Create a registration extension inside `src\Remote\Thiccdal.Remote.<Platform>\`.
2. Expose one service method, e.g. `Add<Platform>Integration(IConfiguration)`, that owns:
   - options binding
   - options validation for external endpoint URIs / polling intervals
   - named or typed HTTP client registration
   - adapter service registration
   - singleton forwarding to shared interfaces
   - platform-specific connection monitor registration
3. Expose one endpoint method, e.g. `Map<Platform>Endpoints()`, that owns OAuth callbacks or webhook routes.
4. Keep feature modules platform-agnostic; they should register their own services, not concrete remote adapters.
5. Test both service registration and callback behavior from the platform test project.
6. If the integration talks to more than one external boundary (for example OAuth + API), register separate named clients instead of one catch-all client so future rewrites can swap one boundary without touching the others.

## Why

This keeps `Program.cs` explicit but slim, and it makes each adapter responsible for its own external boundary. It also prevents unrelated modules from taking direct project references on remote implementations.

## Twitch Example

- `src\Remote\Thiccdal.Remote.Twitch\TwitchRegistrationExtensions.cs`
- `src\Thiccdal\Program.cs`
- `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchRegistrationExtensionsTests.cs`
- Named clients: `Twitch.OAuth`, `Twitch.Helix`
