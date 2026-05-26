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
