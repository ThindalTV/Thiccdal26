# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Kaylee owns backend services, persistence, and bot-side execution paths.

## Recent Updates

📌 Firefly squad configured on 2026-05-27

## Learnings

- Backend work will center on Blazor-hosted services, EF Core data, and bot handlers.
- Twitch bot behavior sits inside the larger streaming control architecture.

### 2026-05-28: Helix Redesign Data Model Changes — Kaylee Lead on Infrastructure

**Team update from Mal (Lead):**
- Helix EventSub architecture locked (see `docs/architecture/helix-redesign.md`).
- Infrastructure contracts must expand to support structured chat rendering and typed events.

**Kaylee's responsibilities — Infrastructure (Phase 18, 1 issue directly assigned):**
- #172: Define ChatFragment hierarchy in Infrastructure (abstract ChatFragment with TextFragment, EmoteFragment, CheermoteFragment subtypes)
- #173: Extend ChatEvent with Fragments, Color, Badges (backward-compatible with ChatEvent.Content as plain-text fallback)

**Kaylee's responsibilities — Data Entities (cross-Phase, in prerequisite issues #10, #11):**
- PlatformEvent entity table (required before Phase 19 event persistence work)
- PlatformUser entity table (required before event ownership)
- These should complete before Phase 19 (#183 event persistence wire-up)

**Kaylee's shared responsibility — Event Bus (Phase 19, 1 issue directly assigned):**
- #183: Wire events to IEventBus + persist before dispatch (Kaylee owns data persistence + event bus plumbing; River owns event mapping)

**Data model decisions locked:**
- ChatEvent.Content remains as plain-text fallback (backward compatibility)
- ChatEvent gains Fragments list (IReadOnlyList<ChatFragment>), Color field (string?), Badges list
- New PlatformEvent subtypes: TwitchFollowEvent, TwitchSubscribeEvent, TwitchCheerEvent, TwitchRaidEvent, TwitchRedeemEvent
- All events stored in PlatformEvent table before dispatch (persistence-before-dispatch pattern)
- ChatFragment serialized as JSON via EF Core value converters (flexible schema, queryable in SQLite)

**Emote CDN caching (optional enhancement):**
- In-memory LRU cache for emote URL lookups (improves rendering performance, reduces Twitch API calls)
- Configurable via IOptions<TwitchOptions> (optional; MVP can skip if simple enough)

**GitHub labeling:** All Kaylee's issues routed with `squad:kaylee` label (28 issues total). Infrastructure work enables River (Phase 17) before Inara rendering work (Phase 18).
