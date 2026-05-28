---
name: "adapter-data-store-seam"
description: "Keep remote adapters decoupled from Thiccdal.Data by pushing persistence behind infrastructure-owned store and event seams."
domain: "integrations"
confidence: "high"
source: "earned"
---

## Context

Use this when a remote/platform adapter needs tokens, connection metadata, or event persistence, but the project boundary says the adapter should not depend directly on `Thiccdal.Data`.

## Patterns

- Define platform-specific persistence seams in `Thiccdal.Infrastructure`, not in the remote adapter and not in `Thiccdal.Data`.
- Let `Thiccdal.Data` implement those seams and register the implementations from the data project.
- Keep runtime normalization in the adapter (`ChatEvent`, typed `PlatformEvent` records, `RawEvent`) and hand persistence off through `IEventBus` / `IChatPersistenceService`.
- When raw payloads participate in downstream identity resolution, store the serialized **item** payload, not a batch envelope.
- During review, prefer the repo's established typed-`HttpClient` + abstraction pattern over old backlog wording that requires a vendor SDK package.

## Examples

- `src\Thiccdal.Infrastructure\YouTube\IYouTubeTokenStore.cs`
- `src\Thiccdal.Data\YouTubeTokenStore.cs`
- `src\Remote\Thiccdal.Remote.YouTube\YouTubeTokenManager.cs`
- `src\Remote\Thiccdal.Remote.YouTube\YouTubeService.cs`
- `src\Thiccdal.Data\ChatPersistenceService.cs`
- `src\Thiccdal.Data\PlatformUserIdResolver.cs`

## Anti-Patterns

- Adding `Thiccdal.Data` as a direct project reference from a remote adapter just to save/read tokens.
- Injecting `ApplicationDbContext` or EF entities directly into adapter classes.
- Building EF `ChatMessage` or `PlatformEvent` entities inside the adapter instead of emitting runtime models through the shared seams.
- Serializing the full poll response into every event when downstream code expects item-level raw payloads.
