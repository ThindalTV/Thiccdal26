# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Mal leads cross-cutting decisions and reviewer gates for the Firefly squad.

### Archived Context (2026-05-26 through 2026-05-28)

**Phase 6 YouTube (2026-05-30):** YouTube adapter aligned with Twitch refactored data strategy. Issues #34–40 validated closable. Phase 6 complete.

**AI Reply Routing (2026-05-30):** AI responses route ONLY to originating platform/channel (no broadcast). Incoming chat mirroring remains passive aggregation.

**Event Bus Decision (2026-05-29):** Defer to Phase 19. EventHandler<T> sufficient through Phase 18. Phase 17 already large.

**Helix EventSub Redesign (2026-05-28):** Pure EventSub WebSocket, ChatFragment hierarchy, deterministic Emote CDN, inline OAuth. 152 issues analyzed, squad-routed, Phase 17–20 staged.

**Repository Structure (2026-05-26):** On-disk layout aligned with architecture. All modules, platforms, tests present and correctly placed.

## Recent Updates

📌 **Phase 10 Question Flash Scope** — 2026-05-28  
   Dashboard + prompter attention flash complete. Tests passing. Ready for operator validation.

📌 **Offline Dashboard Scope Approved** — 2026-05-28  
   Completed architectural review for development-only offline dashboard shortcut. Approved as safe, low-risk convenience feature. Implementation assigned to Inara. Decision recorded in `.squad/decisions.md`.

📌 **Phase 11 Remediation Complete** — 2026-05-27
   Overlay architecture converged on established module boundaries. `IOperatorStateService` seam introduced; dynamic component registration via `IOverlayService`. #107 naming caveat documented; remaining gap isolated to wording drift. Build ✅ clean; 185 tests ✅ passing.

📌 Firefly squad configured on 2026-05-27
📌 Orchestration and decision logs created on 2026-05-26

## Learnings

### 2026-05-27: AI Chatter Memory Decision

**Question asked:** Can the AI responder keep a memory of individual chatters, and what is the safest thin-slice design?

**Current state confirmed:**
- `src\Modules\Thiccdal.Modules.ChatBot\Services\ChatBotAiResponder.cs` builds a minimal two-message `AiChatCompletionRequest` and currently has no memory seam of its own.
- `src\Thiccdal.Data\ChatPersistenceService.cs` already persists every `ChatEvent` into `PlatformEvents`, `ChatMessages`, and `PlatformUsers`, with identity resolved per platform by `src\Thiccdal.Data\PlatformUserIdResolver.cs`.
- `src\Thiccdal.Infrastructure\Bot\Models\PlatformEvent.cs` already carries `Source` and `Channel`, so memory can and should be scoped to platform + channel, not just display name.

**Recommendation:**
- Yes: support chatter memory through a repo-owned `IChatterMemoryService` seam, with `Thiccdal.Data` as the persistence/query implementation.
- Thin slice: derive a short bounded memory summary from existing `ChatMessages` + `PlatformUsers` first; do not introduce vendor-managed memory, vector search, or raw transcript replay.
- `ChatBotAiResponder` should fetch that summary and append it as an extra system/context message before calling `IChatCompletionClient`; keep `IChatCompletionClient` unchanged.

**Safety rules locked:**
- Key memory by `{PlatformEventSource, Channel, PlatformUserId}`; never merge cross-platform identities by display name.
- Store only compact reusable facts/preferences from public chat, with TTL/length caps and an operator clear/reset path.
- Keep raw chat logs as the source of truth; a later summary table is optional only if prompt size/latency becomes a problem.

### 2026-05-30: Phase 6 YouTube Final Reviewer Gate

**Work completed:**
- Ran the final strict reviewer pass for Phase 6 against the revised Twitch-aligned data strategy and the close-out target on issues `#35` and `#40`.
- Verified current repo state with `dotnet build Thiccdal.slnx --no-restore`, `dotnet test src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\Thiccdal.Remote.YouTube.Tests.csproj --no-restore`, and `dotnet test src\Tests\Thiccdal.Tests\Thiccdal.Tests.csproj --no-restore --filter YouTube`.
- Recorded the closure verdict in `.squad/decisions/inbox/mal-phase6-final-review.md`.

**Key findings:**
- `src\Remote\Thiccdal.Remote.YouTube\Thiccdal.Remote.YouTube.csproj` stays Infrastructure-only and `src\Remote\Thiccdal.Remote.YouTube\YouTubeTokenManager.cs` persists tokens through `src\Thiccdal.Infrastructure\YouTube\IYouTubeTokenStore.cs`, keeping the adapter out of `Thiccdal.Data`.
- `src\Remote\Thiccdal.Remote.YouTube\YouTubeLiveChatMessageMapper.cs` now preserves per-item raw JSON and `SourceEventType`, which matches `src\Thiccdal.Data\PlatformUserIdResolver.cs` and `src\Thiccdal.Data\PlatformEventRecordFactory.cs`.
- `src\Remote\Thiccdal.Remote.YouTube\YouTubeService.cs` now leaves the no-broadcast path in `Error`, retries poll failures, and the YouTube test project is green, so the former `#35`/`#40` blocker is cleared.
- `src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\YouTubeTestData.cs` and `src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\YouTubeLiveChatMessageMapperTests.cs` provide the expected Phase 6 mapping matrix, and `Thiccdal.slnx` includes the YouTube test project.

**Reviewer conclusion:**
- Under the revised reviewer basis, issues `#34`, `#35`, `#36`, `#37`, `#38`, `#39`, and `#40` are all honestly closable now.
- Stale issue wording about mandatory Google SDK usage, Data-owned runtime types, or direct adapter persistence should not be used to hold Phase 6 open.

### 2026-05-30: AI Reply Routing Decision (Issue #92)

**Question asked:** Should AI mention replies broadcast to all connected platforms (matching incoming chat mirroring behavior), or stay scoped to the originating platform?

**Current state confirmed:**
- Incoming chat from all platforms is aggregated via `ChatAggregationService` and fanned out to subscribers (passive mirroring for operator visibility)
- AI replies are sent via `ICommandResponseSink` → `ChatServiceCommandResponseSink` → `IChatService.SendMessage()`
- `ChatAggregationService.SendMessage()` broadcasts to ALL connected `IPlatformConnection` instances (Twitch + YouTube + Discord + etc.)
- `CommandContext` only has a display `Platform` string; no routable target or channel identifier

**Recommendation:** AI responses must reply ONLY to the originating platform/channel. Do NOT broadcast.

**Key rationale:**
- Inbound chat mirroring is **passive monitoring** (operator sees unified chat); outbound replies are **active engagement** (different concern)
- Broadcasting AI replies creates spam/confusion for users on platforms where they didn't interact with the bot
- Platform ToS risk (cross-posting bots)
- Attribution confusion (users expect replies where they asked)
- Safety (problematic AI content is scoped, not amplified)

**Implementation strategy:**
1. Add `SourcePlatform` (PlatformEventSource) and `ChannelId` (string?) to `CommandContext`
2. Add scoped-send overload to `IChatSource`: `SendMessage(string message, string? channelId, CancellationToken)`
3. Update `ChatServiceCommandResponseSink` to resolve the originating platform and call ONLY that platform's `SendMessage()`
4. Update `ChatEvent` to include `ChannelId` property; update platform adapters to populate it
5. Update `CommandDispatcher.CreateContext()` to populate new fields from `ChatEvent`

**Nuanced rule preserved:**
- **Incoming chat:** Continue mirroring across all platforms (passive aggregation for operator visibility)
- **Outgoing bot replies:** Route ONLY to originating platform/channel (active engagement, respect boundaries)

**Decision file:** `.squad/decisions/inbox/mal-ai-routing-decision.md`

**Status:** Architecture decision complete. Ready for implementation (Kaylee: interface/routing updates; River: platform adapter changes).

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

### 2026-05-29: Phase 6 YouTube Reviewer Gate

**Work completed:**
- Converted the earlier YouTube audit into an issue-by-issue reviewer checklist for `#34-#40`.
- Ran verification against current repo state: `dotnet build Thiccdal.slnx --no-restore` and targeted YouTube tests both passed.
- Recorded the lead gate in `.squad/decisions/inbox/mal-phase6-plan.md`.

**Key findings:**
- `src\Remote\Thiccdal.Remote.YouTube\Thiccdal.Remote.YouTube.csproj` still references `Thiccdal.Data`, so issue `#34` is not closable against its original contract.
- `src\Remote\Thiccdal.Remote.YouTube\YouTubeLiveChatMessageMapper.cs` serializes the entire poll payload into each event; this breaks `src\Thiccdal.Data\PlatformUserIdResolver.cs`, which expects item-level `authorDetails` at the raw payload root.
- `Thiccdal.Remote.YouTube.Tests` exists and passes, but it is still not listed in `Thiccdal.slnx`, and no `YouTubeTestData` helper exists, so issue `#40` remains open.
- `docs\architecture\overview.md` remains the architecture baseline for remote adapter boundaries and normalization expectations.

### 2026-05-31: Development-Mode Offline Dashboard Scope Review

**Request:** Add a button to access the live dashboard UI without actually going live (for offline development).

**Safety assessment completed:**
- ✅ `IOperatorStateService` seams are SUFFICIENT — no new interfaces, enums, or database queries needed
- ✅ State transition is isolated from `GoLiveActionService` (real Go Live workflow) and platform connections
- ✅ Fully reversible via existing "Go Offline" button (`SetActiveStreamState(null)`)
- ⚠️ CRITICAL: Must be visually distinct and development-only; NOT a broadcast path

**Architecture confirmed:**
- `OperatorStateService.SetActiveStreamState()` can initialize a synthetic `OperatorStreamState` with test metadata
- TopBar mode gate (`_mode == OperatorMode.Live` vs. PreLive) controls entire dashboard layout
- No platform, streaming, or recording services triggered by mode change alone
- Dashboard panels read from `IOperatorStateService`; no downstream platform calls

**Decision recorded:** `.squad/decisions/inbox/mal-offline-dashboard-scope.md`

**Key file paths involved:**
- `src\Thiccdal.Infrastructure\Operators\IOperatorStateService.cs` — service contract (no changes)
- `src\Thiccdal.Infrastructure\Operators\OperatorStateService.cs` — implementation (single synthetic state helper method)
- `src\Modules\Thiccdal.Modules.Control\Components\TopBar\TopBar.razor` — add conditional dev button
- `src\Tests\Thiccdal.Tests\TopBarTests.cs` — two new test cases (entry + exit paths)

**Pattern for reuse:** Offline development shortcuts use existing state service seams without new contracts. Isolation from external services is the key safety gate.

**Reviewer conclusion:**
- None of `#34-#40` are honestly closable yet.
- The fastest follow-up wins are `#35` (explicit no-broadcast error state), `#39` (complete `YouTubeOptions` OAuth docs), and `#40` (solution wiring + helper + full mapping matrix).

### 2026-05-27: Phase 6 Data Strategy Gate Revision

**Work completed:**
- Updated the Phase 6 reviewer gate to match ThindalTV's explicit direction: no hard `Thiccdal.Data` dependency in the YouTube adapter and review against the current remote/data seams, not stale issue wording.
- Wrote the correction gate to `.squad/decisions/inbox/mal-phase6-data-gate.md`.
- Re-ran current verification: `dotnet build Thiccdal.slnx --no-restore` succeeded; `dotnet test src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\Thiccdal.Remote.YouTube.Tests.csproj --no-build` failed in `WhenPollingFails_ThenStateTransitionsToError`.

**Key findings:**
- `src\Remote\Thiccdal.Remote.YouTube\Thiccdal.Remote.YouTube.csproj` is already back to an Infrastructure-only dependency boundary.
- The correct YouTube persistence seam is `IYouTubeTokenStore` in `src\Thiccdal.Infrastructure\YouTube\IYouTubeTokenStore.cs`, implemented by `src\Thiccdal.Data\YouTubeTokenStore.cs`; River should not bypass that seam.
- The correct chat persistence path is `YouTubeService` → `IEventBus` → `ChatPersistenceService`, with item-level `RawData` preserved so `src\Thiccdal.Data\PlatformUserIdResolver.cs` can read `authorDetails.channelId`.
- Current remaining blocker is behavioral, not structural: `src\Remote\Thiccdal.Remote.YouTube\YouTubeService.cs` can set `Connected` after the poll loop already set `Error`, which is why `src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\YouTubeServiceTests.cs` is red.

**Reviewer conclusion:**
- Judge `#34`, `#36`, `#37`, `#38`, and `#39` against the revised gate, not the old issue wording about direct Data types, direct EF writes, or mandatory Google SDK usage.
- Hold `#35` and therefore `#40` open until the polling-state race is fixed and the YouTube test project is green.

### 2026-05-27: Chatter Memory Revision & Operator Control Design

**Work completed:**
- Identified Jayne's two core blockers on chatter-memory implementation:
  1. Missing operator-facing reset UI path
  2. Destructive clear semantics (data loss risk)
- Designed non-destructive reset using ChatterMemoryReset marker table (stores reset timestamps per scope or global)
- Preserved existing memory derivation architecture (derives from ChatMessages + PlatformUsers, no new persistent storage)
- Updated IChatterMemoryService interface: removed Clear/ClearAll (destructive), added Reset/ResetAll (non-destructive)
- Implemented /chatbot Blazor page with scoped + global reset controls, wired to service methods
- Verified all security guardrails remain intact (6-tuple scoping, public-facts-only, no cross-platform merging, no RawData/HtmlContent leakage, AI routing preserved, reset non-destructive)

**Design rationale:**
- Non-destructive reset is minimal adjustment; existing architecture derives memory from persisted chat, reset-marker design keeps that intact
- Avoids data loss while enabling immediate memory suppression for operators
- Reset timestamps enable auditable barrier to memory derivation; source records preserved for recovery + audit trail
- Operator gains real, discoverable path to manage memory without destructive consequences

**Key changes:**
- Thiccdal.Infrastructure/Bot/IChatterMemoryService.cs: Reset/ResetAll interface
- Thiccdal.Data/ChatterMemoryService.cs: Marker storage + filtering logic
- Thiccdal/Components/Pages/Chatbot.razor: Operator UI with reset controls
- Thiccdal/Components/Layout/NavMenu.razor: Chatbot nav entry
- Orchestration log: .squad/orchestration-log/2026-05-27T22-55-44Z-mal-chatter-memory-revision.md

**Status:** Revision complete; ready for Jayne's security re-review.

### 2026-06-01: Phase 10 Question Flash Scope

**Question asked:** What is the acceptance slice for the dashboard/prompter question-attention flash after the user directive to skip dashboard chat feed?

**Scope locked:**
- Dashboard stays focused on the queue; do **not** add a duplicate chat feed to `/dashboard` because `/prompter` already owns operator chat visibility.
- Feature-complete dashboard flash = a transient attention treatment on `QuestionQueuePanel` when the waiting-question count increases.
- Feature-complete prompter flash = the same new-question attention behavior remains on `/prompter`, while the existing significant-event flash stays separate.

**Current seams to reuse:**
- `src\Thiccdal.Infrastructure\Questions\QuestionOverlayService.cs` is the source of truth for queue mutations and raises `StateChanged` after enqueue/add/select/promote/dismiss/clear operations.
- `src\Thiccdal.Infrastructure\Operators\OperatorStateService.cs` already forwards question state through `GetQuestionState()` and rebroadcasts `StateChanged`, which is the right dashboard seam.
- `src\Modules\Thiccdal.Modules.Teleprompter\Pages\Prompter.razor` already implements the desired detection pattern: cache last waiting count, trigger only on increases, and use a versioned async flash reset.

**User preference captured:**
- Keep chat on the prompter; use flash as the dashboard's attention cue instead of duplicating chat UI.

**Likely review/test surfaces:**
- `src\Tests\Thiccdal.Tests\QuestionOverlayServiceTests.cs`
- `src\Tests\Thiccdal.Tests\OperatorStateServiceTests.cs`
- `src\Tests\Thiccdal.Tests\ActivityFeedServiceTests.cs`
- `src\Tests\Thiccdal.Tests\RouteRenderingTests.cs`

### 2026-05-28: Phase 10 Question Flash Acceptance Slice

**Work completed:**
- Defined acceptance slice for dashboard + prompter question attention flash.
- Scope document captured.
- Extracted reusable operator-attention-flash skill.

**Scope locked:**
- Dashboard question-queue flash on new event
- Prompter attention circuit notification
- Dashboard chat feed deferred (prompter owns operator chat visibility)

**Coordination:**
- Inara implements dashboard flash + prompter notification
- Tests: all passing with no regressions
- Orchestration logs: `2026-05-28T00-16-16Z-mal.md` and `2026-05-28T00-16-16Z-inara.md`

**Status:** ✅ Phase 10 increment closed. Ready for operator validation.
