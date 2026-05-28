# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Inara owns the Blazor operator experience, UX polish, and UI ergonomics.

**Key design principles:**
- Operator UI is tablet-oriented; all interactions must be touch-friendly (44px minimum targets)
- The control, overlay, and teleprompter modules are separate UI surfaces
- State-driven architecture: events trigger UI updates, no polling
- Generic components stay presentational; platform-specific logic lives in consumers
- Disabled controls should look deliberate and trustworthy, not broken

**Known constraints:**
- Emote URLs must be whitelisted to `static-cdn.jtvnw.net` (XSS prevention)
- Event type filtering needed to prevent UI clutter
- Emote CDN failover required (fallback to text-based rendering)
- Touch targets cannot become non-interactive based on connection state

**Phase 17–20 responsibilities:**
- Phase 17: Support EventSub integration foundation (no UI changes)
- Phase 18: Emote/badge rendering in Teleprompter and Overlay (#176, #177, #178)
- Phase 19: Event-driven effects (gold flash on significant events) (#185)
- Phase 20: Helix metadata in operator UI (#187)

**Platform integration pattern:**
- All platforms use `IntegrationConnector` + `IntegrationAuthDialog` pair
- State mapped via `IIntegrationConnectionMonitor` / `I{Platform}Service` 
- Each platform adds: injection + state mapping + event subscription
- Enabled: Twitch (Phase 16). Planned: YouTube, Discord, Facebook, X, LinkedIn, TikTok

## Recent Updates

📌 Firefly squad configured on 2026-05-27  
📌 Twitch auth + integration surface complete on 2026-05-29  
📌 Disabled integration chips added on 2026-05-26T23:42:22Z

## 2026-05-27–2026-05-29: Helix API Analysis & Integration Surface Build (Archived Summary)

**Work completed (Inara lead):**
- Analyzed Helix EventSub requirements for emote, badge, and color rendering
- Designed and built `IntegrationConnector` + `IntegrationAuthDialog` component pair (reusable, touch-safe)
- Wired Twitch OAuth flow to Control module top bar
- Fixed duplicate member declarations in `ITwitchService.cs`
- Implemented connected-state interaction (disconnect capability added)
- Added live badge to integration connector
- Built all UI surfaces for Phase 16 completion

**Learnings locked in:**
- Event-driven state (no polling) drives all integration UI updates
- Generic components store no platform-specific logic — makes them reusable
- Touch safety requires consistent 44px+ targets across all states
- Disabled/planned platforms need same visual language as enabled ones (trustworthiness)

**Status:** ✅ All components production-ready, patterns established for future platforms

### 2026-05-29: Disabled Planned Integration Chips in Top Bar

**Implemented:**
- Reused `IntegrationConnector` for non-live platforms by adding an intentional unavailable/disabled presentation path instead of falling back to one-off indicators.
- Replaced the old YouTube/Kick placeholders in `TopBar.razor` with a planned-integration chip list: YouTube, Discord, Facebook, X, LinkedIn, and TikTok.
- Kept Twitch as the only actionable chip and preserved touch sizing while letting the platform row scroll horizontally if the header gets crowded.

**Key UX note:**
- Disabled controls should still look deliberate and trustworthy during live operation: same chip language, same footprint, muted styling, and a short status label like `Soon` or `Pending` rather than feeling like a broken button.

**Architecture / pattern learnings:**
- `IntegrationConnector` can now express two frontend concerns cleanly: actionable connection management and non-actionable planned availability, without moving platform-specific rules into the shared component.
- Approval-gated platforms (currently LinkedIn and TikTok per `docs\architecture\overview.md`) should stay visible in the operator surface but read as intentionally unavailable.

**Key file paths:**
- `src/Modules/Thiccdal.Modules.Control/Components/Integrations/IntegrationConnector.razor`
- `src/Modules/Thiccdal.Modules.Control/Components/Integrations/IntegrationConnector.razor.css`
- `src/Modules/Thiccdal.Modules.Control/Components/TopBar/TopBar.razor`
- `src/Modules/Thiccdal.Modules.Control/Components/TopBar/TopBar.razor.css`

### 2026-05-26: Top-Bar Integration Surface Complete

**Session:** `2026-05-26T23-42-22Z-disabled-integrations`

**Inara completion:** UI enhancement for disabled/planned integration platforms.

**Pattern reuse:** The `IntegrationConnector` component now cleanly expresses both actionable connection management (Twitch active/clicked) and non-actionable planned availability (YouTube, Discord, Facebook, X, LinkedIn, TikTok disabled/muted). No platform-specific logic moved into the shared component.

**Status:** ✅ Production-ready; ready for Phase 17 EventSub foundation.

## Learnings

- The Twitch operator flow should stay on `src\Thiccdal\Components\Pages\TwitchConnect.razor` so channel targeting, authorization, and connection live in one safe operator surface instead of split settings islands.
- `TwitchChatConnectionProfile` is the seam between UI and backend Twitch chat behavior: `BotUsername` is the authenticated bot login, while `TargetChannel` is the broadcaster room the bot joins.
- `ITwitchTargetChannelService` already persists channel overrides and raises `ConnectionProfileChanged`; UI should save through that service instead of writing configuration directly.
- Operators may paste `#channel` or `@channel`; `TwitchTargetChannelService` now normalizes both prefixes before persisting the target channel.
- The `/twitch/connect` route should render inside `DashboardLayout` with `TopBar` so the Twitch entry point feels like part of the control surface instead of falling back to the scaffold admin shell.
- Routed Blazor operator pages should own their own `CancellationTokenSource` lifetime instead of injecting `CancellationTokenSource`, or navigation can update the URL without successfully rendering the destination component.
- Key file paths for the Twitch routing fix: `src\Thiccdal\Components\Pages\TwitchConnect.razor`, `src\Thiccdal\Components\Pages\TwitchConnect.razor.css`, `src\Tests\Thiccdal.Tests\RouteRenderingTests.cs`, and `src\Tests\Thiccdal.Tests\ThiccdalApplicationFactory.cs`.
- Rich Twitch operator surfaces should render from `IActivityFeedService` / `ActivityFeedEntry` instead of binding pages directly to raw chat events; that keeps teleprompter and overlay aligned while the formal event bus is still deferred.
- `ChatMessagePart` + `ChatBadge` is the current rendering seam for Twitch rich chat in this repo, not the older `ChatFragment` plan in the architecture draft; UI should prefer `Parts`/`Badges` and fall back to `Content`.
- Safe Twitch emote rendering should only trust `static-cdn.jtvnw.net` image URLs and keep a plain-text fallback path for stream-safe readability.
- The overnight Twitch rendering slice lives primarily in `src\Modules\Thiccdal.Modules.Teleprompter\Pages\Prompter.razor`, `src\Modules\Thiccdal.Modules.Teleprompter\Components\PrompterLine.razor`, `src\Modules\Thiccdal.Modules.Overlay\Components\ChatView.razor`, `src\Remote\Thiccdal.Remote.Twitch\TwitchEventSubClient.cs`, and `src\Remote\Thiccdal.Remote.Twitch\TwitchEventSubNotificationMapper.cs`.
- `QuestionDashboardState.AttentionSequence` is the shared UI-safe signal for new-question attention across `QuestionQueuePanel` and `/prompter`; it advances only when a question is newly queued, not on select/promote/dismiss mutations.
- Phase 10 question attention should stay on the existing queue + prompter surfaces; do not add a separate dashboard chat feed when the prompter already owns live chat focus.
- Key file paths for the shared question-attention pass: `src\Thiccdal.Infrastructure\Questions\QuestionDashboardState.cs`, `src\Thiccdal.Infrastructure\Questions\QuestionOverlayService.cs`, `src\Modules\Thiccdal.Modules.Control\Components\Questions\QuestionQueuePanel.razor`, `src\Modules\Thiccdal.Modules.Control\Components\Questions\QuestionQueuePanel.razor.css`, `src\Modules\Thiccdal.Modules.Teleprompter\Pages\Prompter.razor`, `src\Modules\Thiccdal.Modules.Teleprompter\Pages\Prompter.razor.css`, and `src\Tests\Thiccdal.Tests\RouteRenderingTests.cs`.

### 2026-05-28: Phase 10 Question Flash Implementation

**Work completed:**
- Implemented dashboard question-queue flash on new event
- Implemented prompter attention circuit notification
- Updated integration and unit tests; all passing
- Validation: `dotnet test .\\Thiccdal.slnx --verbosity minimal` confirms no regressions
- Deferred dashboard chat feed (prompter owns operator chat visibility)

**Status:** ✅ Phase 10 increment complete. Ready for operator validation.

### 2026-05-28: Issue #129 — Chat Display Name Canonicalization (UI Render Seam)

**Problem:** Overlay chat displayed duplicate or non-canonical viewer names when merged viewer identities persisted as canonical names but UI layer read raw platform author.

**Solution implemented:**
- Refactored `PrompterLine.razor` to consume `ChatMessagePart` normalized display names instead of raw `Author`
- Updated `ChatView.razor` (overlay) to use same canonical `ChatMessagePart` / `ChatBadge` seam
- Added safe fallback to plain-text when emote/badge CDN unavailable
- Touch-safe rendering targets (44px+) preserved

**Integration point:** Downstream of Kaylee's backend persistence fix in `TwitchEventSubNotificationMapper`. Backend now sets canonical display names at event-mapping time; UI layer reads and renders those normalized names.

**Status:** ✅ UI seam complete. Tests pass. Awaiting full integration validation with Kaylee's backend changes.

**Key files modified:**
- `src/Modules/Thiccdal.Modules.Teleprompter/Components/PrompterLine.razor`
- `src/Modules/Thiccdal.Modules.Overlay/Components/ChatView.razor`
