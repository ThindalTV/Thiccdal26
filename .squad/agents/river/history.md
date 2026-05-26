# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

River handles platform adapters, integration seams, and external API contracts.

## Recent Updates

📌 Firefly squad configured on 2026-05-27

## Learnings

- Platform adapters implement shared infrastructure contracts and feed typed events into the system.
- Twitch work belongs with River unless it is purely UI or backend service wiring.
- Current Twitch code lives in `src\Remote\Thiccdal.Remote.Twitch\` and is still a minimal IRC adapter: `TwitchService` only parses `PRIVMSG`, while `TwitchTokenManager` owns token exchange/refresh against Twitch OAuth.
- The architecture target in `docs\architecture\overview.md` expects EventSub-driven typed platform events, persisted chat/event records, and a combined chat+event prompter feed; the current code does not yet provide that seam.
- The prompter path is currently `Modules.ChatBot` -> `IChatService` -> `Modules.Teleprompter\Pages\Prompter.razor`, and `ChatLine.razor` renders plain text only, so emotes/events need normalized fragments before they can reach the streamer-facing view cleanly.
- GitHub Phase 5 Twitch issues (#24-#31) are still useful for routing, but their implementation assumptions are IRC/TwitchLib-centric and should be re-scoped toward Helix + EventSub, with chat work labeled through `area/chatbot` and downstream presentation work routed separately through teleprompter/overlay labels.

### 2026-05-28: Helix EventSub Architecture Locked — River Lead on Phase 17

**Team update from Mal (Lead):**
- Helix EventSub redesign is locked for implementation (see `docs/architecture/helix-redesign.md`).
- Pure EventSub WebSocket (not IRC + EventSub hybrid).
- New data model: ChatFragment hierarchy (TextFragment, EmoteFragment, CheermoteFragment, CheermoteFragment), extended ChatEvent with Fragments/Color/Badges, typed PlatformEvent subtypes.
- 6+ new OAuth scopes required; startup must validate and prompt for re-auth.
- Emote CDN: deterministic Twitch URLs (no HTTP lookup).
- Inline OAuth flow: operator login on first startup, token persisted in SQLite.

**River's Phase 17 ownership (6 issues):**
- #166: Expand TwitchOptions (BroadcasterId, new scopes, EventSub config)
- #167: Implement TwitchHelixClient (typed HTTP client for Helix REST)
- #168: Implement EventSub WebSocket manager (connect, subscribe, reconnect)
- #169: Update OAuth scopes + scope-upgrade re-auth flow
- #170: Define ITwitchEventSubClient in Infrastructure
- #171: Update Thiccdal.Remote.Twitch.Tests scope

**Sequencing:** Phase 17 foundation must complete before Phase 18 (EventSub client is transport). Phase 20 (Helix stream info) can begin in parallel with Phase 18/19.

**GitHub labeling:** All 152 open issues now routed to squad members. Old Phase 5 issues #24–31 closed as superseded.

**Open questions for ThindalTV:**
- Cheer bits threshold for gold flash (suggested: 100)
- Bot mod status in broadcaster channel (affects `moderator:read:followers` scope)
- Animated vs static emotes preference

### 2026-05-29: Twitch Auth + Admin Connection Surface — River

**Scope:** Issue #166 partial (TwitchOptions, scopes, BroadcasterId) + auth/status UI surface.

**What shipped:**
- `TwitchConnectionState` enum (Infrastructure)
- `ITwitchService` upgraded from empty to real contract: `ConnectionState`, `ConnectionStateChanged`, `RefreshConnectionState()`
- `ITwitchTokenManager` upgraded: `HasToken()` (no-throw), `Revoke()`
- `TwitchService` now implements `ITwitchService` with `SetState()` state machine
- `TwitchTokenManager` now has `HasToken()`, `Revoke()`, Phase 17 OAuth scopes
- `TwitchOptions.BroadcasterId` added
- DI: `TwitchService` singleton forwarded to both `IChatSource` and `ITwitchService`
- Admin UI: `PlatformStatusButton.razor`, `TwitchAuthDialog.razor`, `Integrations.razor` at `/integrations`
- Fixed `TopBar.razor` syntax error (orphaned `catch` block) left by Inara's parallel work

**Key files touched:**
- `src/Thiccdal.Infrastructure/Twitch/ITwitchService.cs`
- `src/Thiccdal.Infrastructure/Twitch/ITwitchTokenManager.cs`
- `src/Thiccdal.Infrastructure/Twitch/TwitchConnectionState.cs` (new)
- `src/Thiccdal.Infrastructure/Twitch/TwitchOptions.cs`
- `src/Remote/Thiccdal.Remote.Twitch/TwitchService.cs`
- `src/Remote/Thiccdal.Remote.Twitch/TwitchTokenManager.cs`
- `src/Modules/Thiccdal.Modules.ChatBot/ChatBotRegistrationExtension.cs`
- `src/Thiccdal/Components/Admin/PlatformStatusButton.razor` (new)
- `src/Thiccdal/Components/Admin/TwitchAuthDialog.razor` (new)
- `src/Thiccdal/Components/Pages/Integrations.razor` (new)
- `src/Modules/Thiccdal.Modules.Control/Components/TopBar/TopBar.razor` (fixed)

**Tests:** 22 passing in `Thiccdal.Remote.Twitch.Tests`

**Patterns established:**
- `HasToken()` is always no-throw — safe for component OnInitialized
- Admin UI pattern: `PlatformStatusButton` + `{Platform}AuthDialog` + card on `Integrations.razor`
- DI singleton forwarding: register concrete type once, forward to interfaces via `sp.GetRequiredService<T>()`
- `SetState()` guards no-op transitions and always fires the event

**Inara's parallel work discovered:**
- `IIntegrationConnectionMonitor` / `ITwitchConnectionMonitor` / `TwitchConnectionMonitor` in Infrastructure/Remote — DB token-only check; not yet registered in DI
- `IntegrationConnector.razor`, `IntegrationAuthDialog.razor` in Control module — complementary dashboard widgets that use `ITwitchService` same surface
