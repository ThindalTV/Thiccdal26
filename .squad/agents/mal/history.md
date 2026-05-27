# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Mal leads cross-cutting decisions and reviewer gates for the Firefly squad.

## Recent Updates

📌 **Phase 11 Remediation Complete** — 2026-05-27  
   Overlay architecture converged on established module boundaries. `IOperatorStateService` seam introduced; dynamic component registration via `IOverlayService`. #107 naming caveat documented; remaining gap isolated to wording drift. Build ✅ clean; 185 tests ✅ passing.

📌 Firefly squad configured on 2026-05-27
📌 Orchestration and decision logs created on 2026-05-26

## Learnings

### 2026-05-29: Event Bus Architecture Decision

**Question asked:** Should the project move to an event bus immediately, or stage that change?

**Current state confirmed:**
- Entire event propagation uses C# `EventHandler<T>` delegates on interfaces (`IChatService.OnChatMessageRecieved`, `IChatSource.OnChatMessageRecieved`, `ITeleprompterService.OnScrollRequested`)
- `TwitchService` fires `OnChatMessageRecieved` from its IRC read loop; `ChatView.razor` subscribes directly and unsubscribes in `Dispose()`
- One event type in production today: `ChatEvent`. `RawEvent` exists as a record but is unused at runtime.

**Recommendation:** Do NOT introduce the event bus until Phase 19, exactly as planned in `helix-redesign.md`.

**Key rationale:**
- One event type = no fan-out problem; EventHandler<T> is fit-for-purpose through Phase 18
- Phase 17 is already a large lift (EventSub WebSocket, 3 new EF entities, OAuth refactoring)
- Bus design depends on the full typed event set, which isn't established until Phase 18
- Optional: define `IEventBus` as an interface-only stub in Infrastructure during Phase 17 (no implementation, no wiring) to signal intent and prevent new EventHandler subscriptions for non-chat events

**Decision file:** `.squad/decisions/inbox/mal-event-bus-decision.md`

### 2026-05-28: Helix EventSub Redesign Locked for Phase 17 Implementation

**Work done:**
- Completed comprehensive architecture document: `docs/architecture/helix-redesign.md`.
- Analyzed 152 open GitHub issues and routed to squad members via `squad:` labels (Mal 48, Kaylee 28, Inara 48, River 28).
- Closed Phase 5 IRC issues #24–31 as superseded by Helix redesign.
- Created Phase 17–20 labels and staged 23 new implementation issues.
- Orchestration log: `.squad/orchestration-log/2026-05-28T22-41-14-mal.md`.

**Key architectural decisions locked:**
- Use EventSub WebSocket (pure protocol, no IRC fallback).
- ChatFragment hierarchy for structured chat rendering (TextFragment, EmoteFragment, CheermoteFragment).
- Emote CDN URL strategy (deterministic, configurable static/animated).
- Inline OAuth flow (operator login on first run, token persisted in SQLite).
- Four-phase rollout: Foundation (EventSub ingest) → Rich Chat (fragments + emotes) → Full Events (typed events + event bus) → Stream Info (Helix metadata).

**Data model changes confirmed:**
- ChatEvent gains Fragments list, Color field, Badges list (backward-compat: Content stays as plain-text fallback).
- New typed PlatformEvent subtypes: TwitchFollowEvent, TwitchSubscribeEvent, TwitchCheerEvent, TwitchRaidEvent, TwitchRedeemEvent.
- New interfaces in Infrastructure: ITwitchHelixClient, ITwitchEventSubClient.
- Entity migrations: PlatformEvent table, ChatMessage table, ChatFragment value converters.

**Open questions preserved for user decision:**
- Cheer bits threshold for overlay gold flash effect (suggested default: 100 bits).
- Bot user mod status in broadcaster's channel (affects `moderator:read:followers` scope availability).
- Animated vs static emotes default preference.

**Migration risks and mitigations identified:**
- Stored tokens with old scopes → startup scope validation + re-auth prompt in operator UI.
- EventSub session_reconnect message → WebSocket manager handles reconnect + re-subscribe.
- 6+ API calls per connect for subscriptions → all idempotent (check before create).

**GitHub issue routing completed:**
- Inara: 48 issues (frontend/UX: operator UI, overlay, teleprompter)
- Kaylee: 28 issues (backend: data, chat, APIs, streaming, infrastructure)
- Mal: 48 issues (testing/architecture: type/test, area/tests, cross-cutting)
- River: 28 issues (integrations: platform adapters, phases 5–7e)
- Zero duplicate assignments after cleanup pass.

### 2026-05-28: Helix Redesign Brief Written

**Work done:**
- Created comprehensive architecture document: `docs/architecture/helix-redesign.md`.
- Document covers: motivation (IRC → EventSub transition), current-state problems (no emote data, config-based auth), architectural decision (pure EventSub WebSocket), auth approach (inline OAuth), impacted projects, data model (PlatformEvent, ChatMessage, ChatFragment hierarchy), phased implementation (Phases 17–20), migration/compatibility, open questions, and links/references.
- Preserved user directives: inline Twitch auth (not config-based), and open questions (bot mod status, cheer threshold, animated vs static emotes).
- Document is ready for repo readers and developers on Phase 17+.

**Key architectural decisions locked:**
- Use EventSub WebSocket (pure protocol, no IRC fallback in initial rollout).
- ChatFragment hierarchy for structured chat rendering.
- Emote CDN URL strategy (deterministic, configurable static/animated).
- Inline OAuth flow (operator login on first run, token persisted in SQLite).
- Four-phase rollout: Foundation (EventSub ingest) → Fragments (emote rendering) → Coverage (full events + event bus) → Stream Info (Helix API metadata).

**Open questions preserved for user decision:**
- Bot moderator status in broadcaster's channel (affects `moderator:read:followers` scope availability).
- Cheer bits threshold for overlay gold flash effect.
- Default emote format (animated vs static).

**File created:** `docs/architecture/helix-redesign.md` (17.8 KB).

### 2026-05-28: GitHub Issue Routing + Twitch Helix Architecture

**Work done:**
- Created `squad:zoe` GitHub label (was missing).
- Applied `squad:river` to 54 issues (all platform-specific phases 5–7e including Null platform tests).
- Applied `squad:kaylee` to 61 issues (all backend/data/infrastructure/streaming/chatbot/API/hardening).
- Applied `squad:inara` to 39 issues (all operator UI, overlay, teleprompter, and UI-side tests).
- Closed issues #24–31 (Phase 5 IRC-based Twitch implementation) as superseded by Phase 17–20.
- Created Phase 17–20 labels and 23 new issues for the Helix rewrite.
- Wrote architecture decision to `.squad/decisions/inbox/mal-helix-plan.md`.

**Key findings about current Twitch integration:**
- `TwitchService` uses raw TCP IRC (port 6667), no TLS, no CAP REQ for tags. Plain text only.
- `TwitchTokenManager` is solid — OAuth2 exchange + refresh + DB storage is good pattern to keep.
- `ITwitchService` interface is empty placeholder.
- OAuth scopes are `chat:read chat:edit` only — need ~7 new scopes for full Helix EventSub.
- `ChatEvent.Content` is plain string; no emote, badge, or subscriber data anywhere in the model.
- `Line` model in Teleprompter has `HtmlContent` field that exists but is unused — good hook for emote rendering.
- `ApplicationDbContext` has only `TwitchToken` entity. ChatMessage, PlatformUser, PlatformEvent are all still open issues (10, 11, 14).
- `PlatformEventSource` enum has only Twitch=1 and Null=2 — no YouTube, Discord, etc. yet.

**Architecture decisions made:**
- Use pure EventSub WebSocket (not IRC + EventSub) — single protocol, official Twitch path.
- `channel.chat.message` EventSub subscription replaces IRC PRIVMSG.
- ChatFragment hierarchy: TextFragment, EmoteFragment, CheermoteFragment.
- ChatEvent.Content stays as plain-text fallback for backward compat.
- Emote CDN: `https://static-cdn.jtvnw.net/emoticons/v2/{emoteId}/{default|animated}/dark/1.0` — deterministic, no HTTP call.
- TwitchTokenManager kept as-is; scope validation added at startup.
- Open questions for ThindalTV: cheer bits threshold for flash, animated vs static emotes preference, broadcaster vs bot token for follow scope.

**File paths confirmed:**
- Twitch service: `src/Remote/Thiccdal.Remote.Twitch/TwitchService.cs`
- Twitch token manager: `src/Remote/Thiccdal.Remote.Twitch/TwitchTokenManager.cs`
- Infrastructure contracts: `src/Thiccdal.Infrastructure/`
- Teleprompter: `src/Modules/Thiccdal.Modules.Teleprompter/`
- Line model: `src/Modules/Thiccdal.Modules.Teleprompter/Models/Line.cs`
- Prompter page: `src/Modules/Thiccdal.Modules.Teleprompter/Pages/Prompter.razor`

- `docs\architecture\overview.md` is the first-stop architecture document.
- Zoe owns GitHub coordination; Ralph owns continuous monitoring.
- Phase-based issue workflow is effective for visibility and sequencing.
- Test-per-project structure is established and ready for extension.

### 2026-05-29: Batch Completion — Cross-Cutting Review + Event Bus Decision

**Work completed:**
- Orchestration review: All 5 agents' parallel work (Inara, Kaylee, River, Jayne) integrated cleanly
- Event Bus decision: Recommendation to defer to Phase 19 (phased plan is correct)
- All 22 tests passing in Remote.Twitch.Tests
- Control module builds cleanly; admin components ready
- No architectural blockers for Phase 17 EventSub foundation

**Key coordination findings:**
- Inara's `IntegrationConnector` + River's `ITwitchService` state machine are complementary (both use same contract)
- Kaylee's `IIntegrationConnectionMonitor` is distinct from River's service state machine (DB check vs. live state)
- Jayne's CSRF/upsert/revoke fixes all in place
- DI pattern (typed + generic forwarding) ready for multi-platform registration

**Status:** ✅ Cross-cutting review passed. Architecture decisions documented. Ready for Phase 17 implementation with no blocking issues.

### 2026-05-26: Repository Structure Review — Repository-Architecture Alignment

**Finding:** The on-disk directory structure is **well-aligned** with the documented architecture. The solution layout mirrors the intended module and platform separation cleanly.

**Structure confirmed:**
- `/src/Thiccdal/` — Blazor Server host (host app, no business logic beyond routing)
- `/src/Thiccdal.Data/` — EF Core DbContext, entities, migrations
- `/src/Thiccdal.Infrastructure/` — Interfaces (IPlatformConnection, IChatService, etc.), enums, value types
- `/src/Thiccdal.API/` — Status endpoint and related controllers (phase 12 work: /status, /status/badge.svg)
- `/src/Thiccdal.Streaming/` — RTMP ingest, fanout, recording
- `/src/Modules/` — Four RCL projects: Control (operator UI), Overlay, Teleprompter, ChatBot
- `/src/Shared/` — Thiccdal.Shared.Components (input primitives, shared models)
- `/src/Remote/` — Eight platform adapters (Twitch, YouTube, Facebook, X, Discord, LinkedIn, TikTok, Null)
- `/src/Aspire/` — AppHost and ServiceDefaults
- `/src/Tests/` — Thiccdal.Data.Tests, Thiccdal.Tests, Remote/Thiccdal.Remote.Twitch.Tests

**Notes:**
- All expected platform adapters present; LinkedIn and TikTok correctly marked as disabled-until-approved.
- Test structure follows convention: test project per source project.
- Solution file (Thiccdal.slnx) reflects the disk layout accurately.
- GitHub Issues: 161 total, well-organized by phase (currently phase 16: Pre-Live Checklist). Phases progress from 11 (Overlay) through 16 (Pre-Live Checklist), with clear feature/test/fix categorization.

**Recommendations for future work:**
- Maintain the phase-based issue workflow; it provides clear visibility and sequencing.
- When adding new modules or platforms, replicate the test-per-project structure.
- Configuration follows IOptions<T> pattern consistently; no IConfiguration magic strings observed.
- Glassmorphic CSS conventions are defined in architecture doc (§6) — future CSS work should reference this.
