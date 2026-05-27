# Squad Decisions

## Active Decisions

### 2026-05-26: GitHub Backlog Baseline Established
**Agent:** Zoe (GitHub Sync / Status / Work Items)  
**What:** Initial GitHub issues scan complete. 50+ open issues across phases 11–16. Phase 16 (Pre-Live Checklist) is current focus with 17 issues. Zero open PRs. No assignees recorded yet.  
**Why:** Establishes backlog visibility and readiness state for squad triage. Required for phase-16 work assignment.

### 2026-05-26: Repository Structure Confirmed
**Agent:** Mal (Lead / Orchestrator)  
**What:** On-disk structure verified against `docs/architecture/overview.md`. All expected modules, platform adapters, test projects present and correctly placed. Configuration pattern (IOptions<T>) consistent throughout. No corrective actions needed.  
**Why:** Validates architecture documentation accuracy and confirms readiness for ongoing feature work. Structure alignment enables confident adoption of established conventions (file-scoped namespaces, interface-driven design, test-per-project pattern).

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
### 2026-05-27: Firefly squad roster adopted
**By:** ThindalTV (via Squad)
**What:** Use a Firefly-based persistent roster for Thiccdal: Mal, Kaylee, Inara, Book, River, Jayne, Zoe, plus Scribe and Ralph.
**Why:** The user explicitly wanted the team identity and responsibilities mapped to Firefly characters for long-term squad use.

### 2026-05-27: Zoe and Ralph responsibilities are distinct
**By:** ThindalTV (via Squad)
**What:** Zoe owns GitHub sync, status reporting, and work-item coordination. Ralph owns continuous backlog monitoring, stalled-work detection, and next-item pickup.
**Why:** This keeps project coordination separate from the persistent queue-monitor role while preserving both members on the roster.

### 2026-05-27: Inline Twitch Authentication Directive
**By:** ThindalTV (via Copilot)
**What:** Use inline Twitch authentication rather than config-based credentials. On first bot startup, open a Twitch login window for authentication, then remember the resulting auth for later runs.
**Why:** User request — captured for team memory; improves UX (no credentials in config file) and security (OAuth flow).

### 2026-05-28: Twitch Helix EventSub Architecture Decision
**By:** Mal (Lead / Orchestrator)
**Requested by:** ThindalTV
**Status:** Approved for implementation planning (Phases 17–20, 23 GitHub issues)
**What:** Replace raw TCP/IRC with pure EventSub WebSocket for Twitch chat and platform events. Introduce ChatFragment hierarchy (TextFragment, EmoteFragment, CheermoteFragment) for structured emote/badge rendering. Implement inline OAuth flow (operator login on first run, token persisted in SQLite). Define typed PlatformEvent subtypes (TwitchFollowEvent, TwitchSubscribeEvent, TwitchCheerEvent, TwitchRaidEvent, TwitchRedeemEvent).
**Why:** 
- Current IRC is insecure (raw TCP, no TLS, no tags), plain-text only (no emotes/badges/events)
- Twitch no longer recommends IRC; EventSub is official path
- Enables rich chat rendering, subscriber/cheerer awareness, event-driven overlays
- Deterministic emote CDN URLs (no HTTP lookup required)
**Key decisions:**
- Pure EventSub only (not IRC + EventSub hybrid for MVP)
- ChatEvent.Content stays as plain-text fallback for backward compatibility
- 6+ new OAuth scopes required; startup validates and prompts for re-auth if needed
- Four-phase rollout: Foundation (EventSub ingest), Rich Chat (fragments + emotes), Full Events (all typed events + event bus), Stream Info (Helix metadata)
**Preserved user directives:** Inline Twitch auth; open questions (cheer threshold, bot mod status, animated vs static default)
**Architecture document:** `docs/architecture/helix-redesign.md`

### 2026-05-28: GitHub Issue Routing and Squad Labeling Complete
**By:** Zoe (GitHub Sync / Status / Work Items)
**Status:** Complete
**What:** Routed all 152 open GitHub issues to appropriate squad members via `squad:` labels. Closed Phase 5 IRC issues #24–31 as superseded by Helix redesign. Created Phase 17–20 labels and staged 23 new Helix implementation issues.
**Routing summary:**
- Inara (Frontend/UX): 48 issues (operator UI, overlay, teleprompter)
- Kaylee (Backend): 28 issues (data, chat, APIs, streaming)
- Mal (Testing/Architecture): 48 issues (type/test, area/tests, area/infrastructure)
- River (Integrations): 28 issues (platform adapters)
**Why:** Enables squad visibility, parallelizes work, clarifies ownership per agent expertise

## Copilot Directives (User Requests Captured)

### 2026-05-27T00:48:51Z: ChatFragment and Platform Event Design Directive
**By:** ThindalTV (via Copilot)
**What:** Keep `ChatFragment` as an internal Twitch integration detail rather than a cross-project contract. Expose platform-agnostic events/handlers for follows, cheers, etc. instead of transport-specific payloads. Expand `PlatformEventSource` to include all supported platforms. Require moderator status for the Twitch bot; show auth/setup error if missing. Cheers surface with threshold of 1; retain amount for future theme selection. Always use animated emotes.
**Why:** User request — design guidance for Phase 17–19 implementation
**Locked in:** helix-redesign.md

### 2026-05-27T00:50:25Z: Touch-First UI Draft Directive
**By:** ThindalTV (via Copilot)
**What:** Treat current admin/control UI as a draft, not a 1:1 blueprint. Maintain visual language consistency but design for touch-first on full-screen Surface Pro–style tablets. All interactions must be tablet-optimized.
**Why:** User request — captured for team design guidance through Phase 18

### 2026-05-27T00:58:33Z: Per-Integration Test Projects Directive
**By:** ThindalTV (via Copilot)
**What:** Keep integration tests in separate test projects per platform. Start with `Thiccdal.Remote.Twitch.Tests`, replicate for YouTube, Discord, etc.
**Why:** User request — captured for test structure governance
**Status:** Implemented in squad working structure

### 2026-05-27T01:01:15Z: Book Handoff — Documentation Directive
**By:** ThindalTV (via Copilot)
**What:** Once Twitch integration and auth work complete, have Book begin user documentation for connecting Thiccdal to Twitch.
**Why:** User request — handoff checkpoint
**Status:** Pending (auth work now complete; Book ready to start)

### 2026-05-27T01:03:30Z: Completion Criteria Directive
**By:** ThindalTV (via Copilot)
**What:** A task is not complete unless relevant code compiles and relevant tests pass. If it cannot be run, it is not done.
**Why:** User request — captured as squad governance
**Status:** In effect (22 tests passing, all modules compile cleanly)

## Implementation Decisions (Work Completed)

### 2026-05-28: Integration Connector + Twitch Auth Admin UI Pattern
**Agent:** Inara (Frontend / UX / UI)
**Status:** Implemented
**What:** Built `IntegrationConnector` + `IntegrationAuthDialog` component pair in `Thiccdal.Modules.Control/Components/Integrations/`. Generic presentational components; platform-specific state mapping lives in consumers. Wired to Twitch in `TopBar.razor` using `ITwitchService.ConnectionStateChanged` for live updates and `ITwitchTokenManager.GetAuthorizationUrl()` for OAuth flow. Created `IntegrationConnectionState` enum (5 states: Unknown, NotConnected, Connecting, Connected, Error) as generic bridge.
**Key decisions:**
1. Generic components stay presentational; platform logic in consumers (reusable for YouTube, Kick, etc.)
2. `IntegrationConnectionState` maps platform-specific enums to UI
3. Event-driven state (no polling); TopBar subscribes and calls `InvokeAsync(StateHasChanged)`
4. Auth via redirect: `Navigation.NavigateTo(authUrl, forceLoad: true)` to existing `/twitch/connect` handler
5. Fixed `ITwitchService.cs` bug: removed duplicate members already in `IChatSource` (CS0108)
**Files:**
- `src/Thiccdal.Infrastructure/Twitch/ITwitchService.cs` (fixed)
- `src/Modules/Thiccdal.Modules.Control/Components/Integrations/IntegrationConnectionState.cs` (new)
- `src/Modules/Thiccdal.Modules.Control/Components/Integrations/IntegrationConnector.razor` (new)
- `src/Modules/Thiccdal.Modules.Control/Components/Integrations/IntegrationConnector.razor.css` (new)
- `src/Modules/Thiccdal.Modules.Control/Components/Integrations/IntegrationAuthDialog.razor` (new)
- `src/Modules/Thiccdal.Modules.Control/Components/Integrations/IntegrationAuthDialog.razor.css` (new)
- `src/Modules/Thiccdal.Modules.Control/Components/TopBar/TopBar.razor` (wired)

### 2026-05-28: Integration Connection Monitor Pattern
**Agent:** Kaylee (Backend Dev)
**Status:** Implemented
**What:** Introduced `IIntegrationConnectionMonitor` in `Thiccdal.Infrastructure.Integrations` as shared contract for per-platform OAuth connection state. Each platform registers one implementation as singleton, exposed via both typed and generic interfaces. Inara can enumerate all platforms via `IEnumerable<IIntegrationConnectionMonitor>`.
**Pattern:**
```csharp
collection.AddSingleton<TwitchConnectionMonitor>();
collection.AddSingleton<ITwitchConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
collection.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
```
**Callback flow:** `/auth/twitch/callback` calls `ITwitchConnectionMonitor.RefreshConnectionState()` after token persist; Blazor circuits subscribed to `ConnectionChanged` are notified before redirect.
**Files:**
- `src/Thiccdal.Infrastructure/Integrations/IIntegrationConnectionMonitor.cs` (new)
- `src/Thiccdal.Infrastructure/Twitch/ITwitchConnectionMonitor.cs` (new)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchConnectionMonitor.cs` (new)
- `src/Modules/Thiccdal.Modules.ChatBot/ChatBotRegistrationExtension.cs` (updated)

### 2026-05-28: Twitch OAuth Auth Flow Hardening — Security Review
**Agent:** Jayne (Security Review)
**Status:** Complete (All Fixes Committed)
**What:** Completed OAuth redirect flow security review and applied hardening:
1. **CSRF State Parameter:** 256-bit URL-safe random state, 10-min TTL in `ConcurrentDictionary`, one-time-use validation, auto-pruned on each new auth URL generation
2. **OAuth Callback Graceful Errors:** Nullable `code`, `state`, `error`, `error_description` params; redirects to `/twitch/connect?error=<reason>` on denied auth or invalid state
3. **Token Accumulation (Upsert):** `StoreToken()` now removes all existing rows before insert (one valid token in DB always)
4. **Revoke API Call:** `Revoke()` posts to `POST /oauth2/revoke` before local DB deletion (5-second timeout, best-effort, logs warnings but never blocks)
**Tests added (all passing):**
- `GetAuthorizationUrl_ContainsStateParameter`
- `GetAuthorizationUrl_EachCallProducesUniqueState`
- `WhenStateWasIssued_ValidateAndConsumeStateReturnsTrue`
- `WhenStateNeverIssued_ValidateAndConsumeStateReturnsFalse`
- `WhenStateConsumedTwice_SecondCallReturnsFalse`
- `WhenTokenAlreadyExists_StoreTokenReplacesIt`
**Deferred Risks (Team Decision Needed):**
- Token encryption at rest (DPAPI) — process account scope decision required
- Duplicate auth dialog cleanup (TwitchAuthDialog.razor vs IntegrationAuthDialog.razor)
- Exception swallowing in TopBar.razor (no `ILogger` injection)
- Exception message leak in TwitchConnect.razor UI
**Files:**
- `src/Thiccdal.Infrastructure/Twitch/ITwitchTokenManager.cs` (interface updated)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchTokenManager.cs` (implementation)
- `src/Thiccdal/Program.cs` (OAuth callback endpoint)
- `src/Tests/Remote/Thiccdal.Remote.Twitch.Tests/TestProject1/TwitchTokenManagerTests.cs`

### 2026-05-29: Twitch Auth + Admin Connection Surface Implementation
**Agent:** River (Integrations)
**Status:** Implemented
**What:** Completed auth/status UI surface for Twitch integration and admin UI connection widget.
**Interface Changes (Infrastructure):**
- `ITwitchTokenManager` — added `HasToken()` (no-throw bool check), `Revoke()`
- `ITwitchService` — upgraded from empty placeholder to: `ConnectionState` property, `ConnectionStateChanged` event, `RefreshConnectionState()` method
- `TwitchConnectionState` — new enum: `NotAuthorized | Authorized | Connecting | Connected | Disconnected | Error`
- `TwitchOptions` — added `BroadcasterId` property (required for EventSub Phase 17)
**Implementation Changes (Remote.Twitch):**
- `TwitchTokenManager` — `HasToken()` DB-backed no-throw check; `Revoke()` removes all tokens; `GetAuthorizationUrl()` expanded to Phase 17 scopes
- `TwitchService` — proper `ITwitchService` implementation with `SetState()` state machine; fires `ConnectionStateChanged` on transitions; `Connected` property derives from state
**DI Registration (ChatBot Module):**
- `TwitchService` singleton forwarded to both `IChatSource` and `ITwitchService` (single instance satisfies both)
**Admin UI (Blazor Host):**
- `PlatformStatusButton.razor` (`Components/Admin/`) — reusable status badge
- `TwitchAuthDialog.razor` (`Components/Admin/`) — Bootstrap modal with Authorize/Revoke
- `Integrations.razor` (`Components/Pages/`) — admin page wiring everything
- Updated `_Imports.razor` and `NavMenu.razor`
**Coordination with Inara:**
- Discovered Inara's parallel `IIntegrationConnectionMonitor` work in Control module (same `ITwitchService` surface)
- Fixed orphaned `catch` block in `TopBar.razor` (broken host build)
- Two complementary surfaces established: admin components (host) + Control module components (dashboard)
**Key Decisions:**
1. `ITwitchService` owns the state machine (single source of truth)
2. Phase 17 scopes added now to avoid second auth flow later
3. Reusable admin pattern: `PlatformStatusButton` + platform-specific dialog
4. `HasToken()` never throws (safe for component `OnInitialized`)
**Files:**
- `src/Thiccdal.Infrastructure/Twitch/ITwitchService.cs` (updated)
- `src/Thiccdal.Infrastructure/Twitch/ITwitchTokenManager.cs` (updated)
- `src/Thiccdal.Infrastructure/Twitch/TwitchConnectionState.cs` (new)
- `src/Thiccdal.Infrastructure/Twitch/TwitchOptions.cs` (updated)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchService.cs` (upgraded)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchTokenManager.cs` (upgraded)
- `src/Modules/Thiccdal.Modules.ChatBot/ChatBotRegistrationExtension.cs` (updated)
- `src/Thiccdal/Components/Admin/PlatformStatusButton.razor` (new)
- `src/Thiccdal/Components/Admin/TwitchAuthDialog.razor` (new)
- `src/Thiccdal/Components/Pages/Integrations.razor` (new)
- `src/Modules/Thiccdal.Modules.Control/Components/TopBar/TopBar.razor` (fixed)

### 2026-05-29: Event Bus Architecture Decision
**Agent:** Mal (Lead / Orchestrator)
**Requested by:** ThindalTV
**Status:** Recommendation — Awaiting Acknowledgement
**What:** Recommendation is to **defer event bus introduction to Phase 19**, exactly as planned in `helix-redesign.md`. Do NOT introduce in Phase 17 or 18.
**Current State Confirmed:**
- Event propagation uses vanilla C# `EventHandler<T>` on interfaces (`IChatService.OnChatMessageRecieved`, etc.)
- One event type in production: `ChatEvent`; `RawEvent` unused
- No fan-out complexity; no mediator or bus
**Rationale for Deferral:**
1. One event type = no fan-out problem; `EventHandler<T>` is fit-for-purpose through Phase 18
2. Phase 17 is already substantial (EventSub WebSocket, 3 new EF entities, OAuth refactoring)
3. Bus design depends on full typed event set (follows, subs, cheers, redeems), not established until Phase 18
4. Current shape survives Phase 17–18 unchanged; EventSub fires same `OnChatMessageRecieved`; subscriber code needs no change
5. `EventHandler<T>` is appropriate for in-process Blazor Server; bus adds indirection without justified complexity
**Optional Phase 17 Prep (Recommended):**
- Define `IEventBus` as interface-only stub in Infrastructure (no implementation, no wiring)
- Low cost; signals intent; prevents new `EventHandler` subscriptions for non-chat events; anchors Phase 19 implementation
**Decision Summary:**
- Move to bus now (Phase 17)? ❌ No
- Move to bus in Phase 18? ❌ No
- Move to bus in Phase 19? ✅ Yes (as planned)
- Define IEventBus interface in Phase 17? ⚠️ Optional but recommended
**Conclusion:** The phased plan in `helix-redesign.md` is correct. Trust the plan.

### 2026-05-29: SQLite Startup Database Initialization
**Agent:** Kaylee (Backend Dev)
**Status:** Implemented and Tested
**What:** Added EF Core migrations as the only startup database initialization path for the Blazor host. On app launch, app resolves `IDbContextFactory<ApplicationDbContext>`, ensures the SQLite directory exists, and calls `Database.MigrateAsync()` before app begins serving requests.
**Why:**
- Recreates deleted SQLite file from real schema instead of `EnsureCreated`, which bypasses migrations
- Matches existing repo pattern of `AddDbContextFactory<ApplicationDbContext>` for singleton-safe DB access
- Handles both missing database file and missing configured parent directory
**Validation:** 28 tests passing; build clean with zero warnings
**Files:**
- `src/Thiccdal.Data/ApplicationDbContextInitializationExtensions.cs` (new)
- `src/Thiccdal/Program.cs` (updated)
- `src/Tests/Thiccdal.Data.Tests/ApplicationDbContextInitializationExtensionsTests.cs` (new)

### 2026-05-29: CVE Fix — OpenTelemetry Package Updates (Aspire Service Defaults)
**Agent:** Kaylee (Backend Dev)
**Status:** Complete
**What:** Updated OpenTelemetry packages in `src/Aspire/Thiccdal.Aspire.ServiceDefaults/Thiccdal.Aspire.ServiceDefaults.csproj` from version `1.14.0` to current non-vulnerable versions:
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` → `1.15.3`
- `OpenTelemetry.Extensions.Hosting` → `1.15.3`
- `OpenTelemetry.Instrumentation.AspNetCore` → `1.15.2`
- `OpenTelemetry.Instrumentation.Http` → `1.15.1`
- `OpenTelemetry.Instrumentation.Runtime` → `1.15.1`
**Why:** `dotnet build` failing with `NU1902` due to active advisories on v1.14.0; no central package management in repo, so fix point is service defaults
**Validation:** `dotnet restore` and `dotnet build` both passing

### 2026-05-29: Stored Token Click Path Fix (TopBar.razor)
**Agent:** Kaylee (Backend Dev)
**Status:** Complete
**What:** Removed broken `_twitchIsAuthorized` field references from TopBar.razor that prevented component compilation. Component now correctly navigates to `/twitch/connect` for all token states.
**Why:** TopBar chip appeared non-interactive when stored token existed. Root cause was incomplete edits leaving undefined field references (lines 64, 72).
**Key Pattern Confirmed:** Control module uses page navigation for auth/connection management (not modals). `/twitch/connect` page handles all states: NotAuthorized → Authorize button, Authorized → Connect button, Connected → Disconnect button.
**Impact:** 27 tests passing; clean build; click path works for all token states; generic pattern supports other platform chips
**Files:** `src/Modules/Thiccdal.Modules.Control/Components/TopBar/TopBar.razor` (fixed)

### 2026-05-29: Twitch Auth-State vs. Live-State Refresh Separation
**Agent:** Kaylee (Backend Dev)
**Status:** Implemented
**What:** Keep Twitch auth-state refresh (token status) separate from live-state refresh (stream metadata via Helix). Top-left TWI connector refresh does NOT wait on Helix stream metadata.
**Why:** Connector needs clickable auth state immediately. Waiting on Helix metadata during `RefreshConnectionState()` can delay chip response long enough that operator experiences UI as unresponsive.
**Pattern Applied:** `ITwitchService.RefreshAuthStateAsync()` (token check) ≠ `RefreshConnectionState()` (Helix metadata); TopBar calls auth-state refresh on startup.
**Files:**
- `src/Remote/Thiccdal.Remote.Twitch/TwitchService.cs` (updated)
- `src/Thiccdal.Infrastructure/Twitch/ITwitchService.cs` (updated)
- `src/Modules/Thiccdal.Modules.Control/Components/TopBar/TopBar.razor` (updated)

### 2026-05-29: Twitch Test Project Structure Correction
**Agent:** Kaylee (Backend Dev)
**Requested by:** ThindalTV
**Status:** Pending Implementation
**What:** Move Twitch test project from nested placeholder to folder root:
- **Current:** `src/Tests/Remote/Thiccdal.Remote.Twitch.Tests/TestProject1/Thiccdal.Remote.Twitch.Tests.csproj`
- **Target:** `src/Tests/Remote/Thiccdal.Remote.Twitch.Tests/Thiccdal.Remote.Twitch.Tests.csproj`
**Why:** On-disk folder structure must mirror solution structure. Placeholder subfolder causes solution entry, assembly name, and test host display name to drift. Moving to folder root stabilizes identity and simplifies project references.
**Next:** Remove TestProject1 subfolder, move project file to parent root, update solution references

### 2026-05-29: Helix Contract Groundwork — Typed Options and Identity Separation
**Agent:** Kaylee (Backend Dev)
**Status:** Implemented
**What:** Locked Twitch Helix/EventSub boundary around typed options and split identities before River reimplements the adapter transport layer.
**Key Changes:**
- `TwitchOptions` now carries `BotUserId`, `OAuthBaseAddress`, and separate `Helix` and `EventSub` sub-options
- `TwitchChatConnectionProfile` now includes both `BotUserId` and `BroadcasterId`
- `AddTwitchIntegration()` owns validation for OAuth, Helix, and EventSub endpoints with separate named `HttpClient` registrations (OAuth vs Helix traffic)
**Why:**
- EventSub subscription APIs need authenticated bot user ID and broadcaster ID independently
- Host/UI code reads one stable, typed config shape instead of inheriting adapter-only constants
- River can rework transport internals without moving `Program.cs` or re-laying DI boundaries again
**Tests:** Host build ✅, Twitch adapter tests ✅
**Files:**
- `src/Thiccdal.Infrastructure/Twitch/TwitchOptions.cs` (updated)
- `src/Thiccdal.Infrastructure/Twitch/TwitchHelixOptions.cs` (new)
- `src/Thiccdal.Infrastructure/Twitch/TwitchEventSubOptions.cs` (new)
- `src/Thiccdal.Infrastructure/Twitch/TwitchChatConnectionProfile.cs` (updated)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchRegistrationExtensions.cs` (updated)

### 2026-05-29: Helix Foundation Slice — ITwitchHelixClient Seam
**Agent:** River (Integrations)
**Status:** Implemented
**What:** Opened the Helix foundation layer inside the Twitch adapter boundary. Introduced dedicated `ITwitchHelixClient` typed seam and moved stream-state + outbound chat paths behind it.
**Key Changes:**
- `ITwitchHelixClient` interface in Infrastructure with REST methods for Helix
- `TwitchHelixClient` implementation owning Helix HTTP calls
- `TwitchService.RefreshStreamState()` now uses typed Helix client
- `TwitchService.SendMessage()` prefers Helix chat send when bot user ID + broadcaster ID available
- IRC retained for current inbound chat/connect behavior and temporary outbound fallback
**Why:**
- Gives adapter real Helix boundary without forcing EventSub or persistence work into same change
- Reduces future churn: EventSub can replace IRC receive later without disturbing typed REST seam
- `TwitchService` already talks to a seam instead of owning raw Helix request construction
**Tests:** Twitch adapter tests ✅, Host build ✅
**Files:**
- `src/Thiccdal.Infrastructure/Twitch/ITwitchHelixClient.cs` (new)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchHelixClient.cs` (new)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchService.cs` (refactored)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchTokenManager.cs` (updated)

### 2026-05-29: Phase 11 Remediation — Overlay Architecture Closure
**Agent:** Mal (Lead / Remediation Owner)
**Status:** Complete
**What:** Completed remediation pass for remaining phase 11 overlay gaps, converging on architecture documented in `docs\architecture\overview.md`. Introduced `IOperatorStateService` as shared multi-session seam for overlay test flashes, question-driven UI reactions, and in-memory Stream Info reminder state. Reworked `/overlay` to render registered overlay components dynamically from `IOverlayService`. Replaced drifted overlay component contracts with `ChatFeedOverlayComponent`, `LowerThirdOverlayComponent`, and corrected `EventTickerOverlayComponent`. Moved prompter flashes to split behavior: cyan for new questions, gold for significant events only. Wired `StreamInfoDialog` into operator top bar; hid LinkedIn/TikTok reminders when disabled.
**Trade-off:** Did NOT relocate overlay project from `src\Modules\Thiccdal.Modules.Overlay` to `src\Thiccdal.Overlay`. Repository structure standardizes on `Modules` location; remaining gap treated as #107 naming/wording drift rather than architectural blocker.
**Why:**
- Functional blockers were behavior and wiring mismatches, not architecture
- `IOperatorStateService` seam enables Phase 11+ multi-operator state without regressing question overlay service
- Narrow isolation: remaining mismatch isolated to #107 naming/path contract, not active behavior drift
**Build Status:** ✅ Clean build, 185 tests passing
**Files:** `src\Modules\Thiccdal.Modules.Control\`, `src\Modules\Thiccdal.Modules.Overlay\`, `src\Thiccdal.Overlay\`, `src\Thiccdal.Infrastructure\`, `src\Tests\`, `docs\architecture\`

### 2026-05-27T19:17:28+02:00: User Directive — Phase 12 and 14 Completion Order
**By:** ThindalTV (via Copilot)
**What:** After phase 11 review fully passes, proceed directly to complete phase 12 entirely, then complete phase 14 entirely.
**Why:** User request — workstream sequencing

### 2026-05-27T19:27:28+02:00: User Directive — Phase 9 Completion After Phase 14
**By:** ThindalTV (via Copilot)
**What:** After phase 14 is complete, proceed to phase 9 and complete that phase as the next workstream.
**Why:** User request — workstream sequencing
