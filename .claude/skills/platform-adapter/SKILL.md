---
name: platform-adapter
description: How to build or modify a platform integration under src/Remote/ — registration extensions, the Infrastructure/Data seam for tokens and persistence, typed HTTP clients, raw payload preservation, and connection monitors. Use when adding a platform, changing an adapter, or wiring an adapter into DI.
---

# Platform adapter patterns

Every chat/streaming platform lives in `src/Remote/Thiccdal.Remote.<Platform>/`. These are the
seams that keep adapters independent of each other, of `Thiccdal.Data`, and of the host.

## Registration extension — one entry point per adapter

Each adapter owns a `<Platform>RegistrationExtensions.cs` exposing two methods:

- `Add<Platform>Integration(IConfiguration)` — options binding and validation, HTTP client
  registration, adapter service registration, singleton forwarding to shared interfaces, and the
  connection monitor.
- `Map<Platform>Endpoints()` — OAuth callbacks and webhook routes.

`Program.cs` stays high-level composition only. Feature modules register their own services and
never take a project reference on a concrete remote adapter.

If the integration talks to more than one external boundary, register **separate named clients**
(Twitch uses `Twitch.OAuth` and `Twitch.Helix`) so one boundary can be rewritten without touching
the other.

Reference: `src/Remote/Thiccdal.Remote.Twitch/TwitchRegistrationExtensions.cs`

## The Infrastructure/Data seam — adapters never touch EF

An adapter must not reference `Thiccdal.Data` or inject `ApplicationDbContext`.

1. Define the persistence seam as an interface in `Thiccdal.Infrastructure/<Platform>/`.
2. Implement it in `Thiccdal.Data`, and register the implementation from the data project.
3. The adapter normalises to runtime models (`ChatEvent`, typed `PlatformEvent` records, raw
   events) and hands off through `IEventBus` / `IChatPersistenceService`.

Reference: `IYouTubeTokenStore` (Infrastructure) → `YouTubeTokenStore` (Data) →
`YouTubeTokenManager` (adapter).

**Anti-patterns:** referencing `Thiccdal.Data` from an adapter just to save a token; building EF
`ChatMessage`/`PlatformEvent` entities inside the adapter.

## Typed HTTP client seam

Put request construction, auth headers, and response DTO parsing in a narrow typed client behind
an Infrastructure interface; leave orchestration in the adapter service.

Reference: `ITwitchHelixClient` (Infrastructure) → `TwitchHelixClient` (adapter) → consumed by
`TwitchService`.

Migrate one path at a time rather than rewriting a whole transport in one change, and keep a
temporary fallback to the legacy path where that preserves behaviour. Test the two layers
separately: client tests cover method/route/headers/payload mapping; service tests cover choosing
the seam and passing the resolved connection profile.

## Raw payload preservation

When normalising polled or pushed platform events, store the serialized **item** payload and the
source event name — not the batch envelope. Downstream identity resolution
(`PlatformUserIdResolver`) reads item-level raw data, and a batch envelope silently breaks it.

## Connection monitors

Platform auth state reaches the UI through `IIntegrationConnectionMonitor`
(`Thiccdal.Infrastructure/Integrations/`):

```csharp
public interface IIntegrationConnectionMonitor
{
    string PlatformName { get; }
    bool IsConnected { get; }
    string GetAuthorizationUrl();
    event EventHandler? ConnectionChanged;
    Task RefreshConnectionState(CancellationToken cancellationToken = default);
}
```

Register the concrete singleton once, then forward it to both the platform-specific interface and
the shared one, so components can inject either the typed monitor or
`IEnumerable<IIntegrationConnectionMonitor>` for platform-agnostic rendering:

```csharp
services.AddSingleton<TwitchConnectionMonitor>();
services.AddSingleton<ITwitchConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
```

Raise `ConnectionChanged` **only when the state actually changes** — spurious events cause
needless Blazor re-renders. Call `RefreshConnectionState` from the OAuth callback after persisting
the token, before redirecting, so subscribed circuits re-render.

## First run is "disconnected", not an error

An adapter with no stored token yet is in an explicit disconnected state. Do not throw on startup
because credentials are absent — model it as state the operator surface can explain.

## Adapters are chat and events only

Thiccdal does not ingest, restream, or record video — OBS publishes to each platform directly.
An adapter surfaces chat, events, and connection state. Do not add relay, fanout, or stream-key
handling to one.

## Testing

`Thiccdal.Remote.Null` provides no-op implementations that log every operation at `Information`
level; use it as the stand-in for a live platform. Note that only `Twitch.Tests` currently exists
under `src/Tests/Remote/` — a new adapter test project must be added to `Thiccdal.slnx` or it will
never run.
