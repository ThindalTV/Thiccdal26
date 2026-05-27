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
- The smallest clean Twitch live indicator seam is a boolean `ITwitchService.IsStreamLive` plus a `StreamLiveStateChanged` event and `RefreshStreamState()`, backed by Helix `/streams` with `BroadcasterId` rather than a larger stream-info model.
- `ITwitchTokenManager.GetToken()` now returns `null` when no Twitch token has ever been stored, and startup paths in `src\Remote\Thiccdal.Remote.Twitch\TwitchService.cs` must treat that as the explicit first-run `NotAuthorized` state instead of throwing.
- The first-run no-token regression is covered in `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchTokenManagerTests.cs` and `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchServiceTests.cs`; real token refresh and network failures still remain exception paths.
- Twitch startup wiring now lives in `src\Remote\Thiccdal.Remote.Twitch\TwitchRegistrationExtensions.cs`; `Program.cs` should call `AddTwitchIntegration(builder.Configuration)` and `MapTwitchEndpoints()` rather than owning Twitch callback or DI details.
- Platform-specific DI ownership belongs in the adapter project, not feature modules; `src\Modules\Thiccdal.Modules.ChatBot\ChatBotRegistrationExtension.cs` now stays platform-agnostic and the module no longer references `Thiccdal.Remote.Twitch`.
- `TwitchConnectionMonitor` is registered from the Twitch adapter beside `ITwitchService` and `ITwitchTokenManager`, so OAuth callbacks can refresh both the service state machine and the generic integration monitor from one boundary.
- Extraction coverage lives in `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchRegistrationExtensionsTests.cs`, alongside the existing Twitch token/service tests.
- Runtime-editable Twitch channel targeting should persist in SQLite, not by rewriting `appsettings.json`; the UI-safe seam is now `src\Thiccdal\Services\TwitchSettingsService.cs` delegating to `src\Remote\Thiccdal.Remote.Twitch\TwitchTargetChannelService.cs`.
- Keep Twitch bot identity and target broadcaster identity explicit: `TwitchOptions.BotUsername` identifies the authenticated bot account, while `DefaultTargetChannel`/`DefaultBroadcasterId` seed the target channel owner until a UI override is saved.
- `TwitchService` should always resolve a `TwitchChatConnectionProfile` before connecting or calling Helix so IRC login uses the bot username while stream-state lookups use the target broadcaster ID; live target switches flow through `ITwitchTargetChannelService.ConnectionProfileChanged`.
- Verification for configurable target-channel work: `dotnet build src\Thiccdal\Thiccdal.csproj`, `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj`, and `dotnet test src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj`.
- Phase 17 Helix foundation now has a dedicated seam: `src\Thiccdal.Infrastructure\Twitch\ITwitchHelixClient.cs` with `src\Remote\Thiccdal.Remote.Twitch\TwitchHelixClient.cs` owning Helix REST calls instead of embedding raw HTTP in `TwitchService`.
- `TwitchService` still keeps IRC receive/connect behavior for now, but `RefreshStreamState()` and `SendMessage()` should prefer the typed Helix client; outbound chat falls back to IRC only when Helix cannot be used yet.
- Twitch config now carries Helix/EventSub foundation knobs in `src\Thiccdal.Infrastructure\Twitch\TwitchOptions.cs`, `TwitchHelixOptions.cs`, and `TwitchEventSubOptions.cs`; moderator-required EventSub and animated emotes are the defaults, and OAuth scopes are configured from options rather than a hard-coded string.
- Coverage for the Helix foundation slice lives in `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchHelixClientTests.cs`, `TwitchServiceTests.cs`, and `TwitchRegistrationExtensionsTests.cs`.

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

### 2026-05-29: Batch Completion — Twitch Auth + Integration Surface

**Team summary:**
- River's `ITwitchService` state machine is the single source of truth for connection state
- Inara's `IntegrationConnector` + `IntegrationAuthDialog` UI components use `ITwitchService` for all state queries
- Kaylee's `IIntegrationConnectionMonitor` is a separate DB-only check for platform enumeration (complementary, not competitive)
- Jayne's CSRF/token hardening all committed; OAuth flow is secure
- Mal confirmed no architectural conflicts; both UI surfaces coexist cleanly

**Key patterns locked:**
- `TwitchService` implements `ITwitchService` with `SetState()` guarding transitions and firing events
- State machine lives at service level; Blazor components subscribe to `ConnectionStateChanged`
- No-throw `HasToken()` check safe for component `OnInitialized`
- Admin pattern: `PlatformStatusButton` + `{Platform}AuthDialog` + card on `Integrations.razor`

**Status:** ✅ 22 tests passing. Twitch auth/status surface production-ready. Phase 17 scopes in place. Ready for EventSub foundation work.

### 2026-05-29: Helix Foundation Slice — ITwitchHelixClient Seam

**Requested by:** Squad coordination (deliver first Helix integration slice after Kaylee's contract work)

**What landed:**
- `ITwitchHelixClient` interface in Infrastructure with REST methods for Helix API calls
- `TwitchHelixClient` implementation (`src\Remote\Thiccdal.Remote.Twitch\`) owns all Helix HTTP calls
- `TwitchService.RefreshStreamState()` now routes through typed Helix client instead of bare HTTP
- `TwitchService.SendMessage()` prefers Helix chat send when bot user ID + broadcaster ID available
- IRC retained only for current inbound chat/connect behavior and temporary outbound fallback

**Why:**
- Gives the adapter a real Helix boundary without forcing EventSub or persistence work into same change
- Reduces future churn when EventSub replaces IRC inbound flow — `TwitchService` already talks to a typed seam instead of owning raw Helix request construction
- Transport layers can evolve independently; `TwitchService` contract stays stable

**Key Files:**
- `src\Thiccdal.Infrastructure\Twitch\ITwitchHelixClient.cs` (new)
- `src\Remote\Thiccdal.Remote.Twitch\TwitchHelixClient.cs` (new)
- `src\Remote\Thiccdal.Remote.Twitch\TwitchService.cs` (refactored to use client)
- `src\Remote\Thiccdal.Remote.Twitch\TwitchTokenManager.cs` (updated)

**Tests:** ✅ Twitch adapter tests, ✅ Host build

**Learnings:**
- Seam-based design prevents transport rewrites from rippling upstream
- Typed clients are preferable to extension methods on generic HttpClient
- RefreshStreamState and SendMessage now have clear Helix-backed paths; IRC is explicit fallback

**Next:** EventSub WebSocket manager can now plug in beside ITwitchHelixClient without disturbing these paths.
- EventSub inbound is now owned by `src\Remote\Thiccdal.Remote.Twitch\TwitchEventSubClient.cs`, with payload mapping isolated in `TwitchEventSubNotificationMapper.cs` and subscription CRUD living in `TwitchHelixClient.cs`.
- Rich Twitch chat should stay platform-agnostic at the consumer boundary: `ChatEvent` now carries `ChatMessagePart`, `ChatBadge`, color, and HTML fallback, while typed follow/subscribe/cheer/raid/redeem records derive from `PlatformEvent`.
- The fastest safe bridge to overlay/prompter is the shared activity feed seam (`IActivityFeedService` + `PlatformActivityFormatter`), which lets Twitch events surface downstream before a formal `IEventBus` exists.
- Raw Twitch payloads are now persisted through `src\Thiccdal.Data\Models\PlatformEventRecord.cs` and dispatched from `TwitchService` only after persistence, giving downstream consumers diagnostics without depending on Helix/EventSub JSON directly.
- Validation for the Helix/EventSub slice currently means: `dotnet build src\Thiccdal\Thiccdal.csproj`, `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj`, and `dotnet test src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj`.
- Phase 6 YouTube now follows the same persistence seam as Twitch: the adapter stays on `Thiccdal.Infrastructure`, publishes via `IEventBus`, and leaves EF/storage ownership inside `src\Thiccdal.Data\`.
- When a normalized event needs both a shared enum and the vendor's exact type string, persist both: `PlatformEvent.Type` stays normalized while `PlatformEvent.SourceEventType` carries values like `textMessageEvent` or `channel.follow`.
- YouTube chat persistence depends on item-level raw JSON with `authorDetails.channelId` at the root; `src\Tests\Thiccdal.Data.Tests\ChatPersistenceServiceTests.cs` is the guardrail for that seam.
- Current integration validation for this slice is: `dotnet build src\Thiccdal\Thiccdal.csproj`, `dotnet test src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\Thiccdal.Remote.YouTube.Tests.csproj`, `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj`, and `dotnet test src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj`.

### 2026-05-29: Phase 6 YouTube completion

- `src\Remote\Thiccdal.Remote.YouTube\` now stays infrastructure-first by consuming `IYouTubeTokenStore` from `src\Thiccdal.Infrastructure\YouTube\`; the adapter project no longer references `Thiccdal.Data` directly.
- For poll-based platforms, preserve per-item raw JSON and set `SourceEventType` on every normalized event so unknown vendor message types can stay diagnosable without misclassifying them as chat.
- YouTube typed runtime events now live in `src\Thiccdal.Infrastructure\Bot\Models\SuperChatEvent.cs` and `MembershipEvent.cs`, while persisted TPH counterparts and migrations live in `src\Thiccdal.Data\Models\` and `src\Thiccdal.Data\Migrations\`.
- Phase 6 verification commands: `dotnet build Thiccdal.slnx --no-restore`, `dotnet test src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\Thiccdal.Remote.YouTube.Tests.csproj --no-restore`, `dotnet test src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj --no-restore`, and `dotnet test src\Tests\Thiccdal.Tests\Thiccdal.Tests.csproj --no-restore --filter "FullyQualifiedName~StatusEndpointTests|FullyQualifiedName~StreamStatusServiceTests|FullyQualifiedName~NullPlatformFullStackTests"`.
