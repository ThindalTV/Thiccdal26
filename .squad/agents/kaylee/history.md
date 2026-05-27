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
- 2026-05-29: Keep `ITwitchService.RefreshConnectionState()` auth-only. If it also waits on Helix live-state lookups, the top-bar Twitch chip can stay non-interactive long enough to feel broken; refresh stream state separately after the auth state is visible.
- 2026-05-29: SQLite startup recovery should run from `src\Thiccdal\Program.cs` via `app.Services.InitializeDatabase(...)`, using `IDbContextFactory<ApplicationDbContext>` plus `Database.MigrateAsync()` so a deleted `thiccdal.db` is recreated from real EF Core migrations.
- 2026-05-29: Keep startup DB init logic in `src\Thiccdal.Data\ApplicationDbContextInitializationExtensions.cs`; ensure the configured SQLite directory exists before migrating so nested `Data Source=` paths work on first launch.
- 2026-05-29: Regression coverage for missing SQLite files belongs in `src\Tests\Thiccdal.Data.Tests\ApplicationDbContextInitializationExtensionsTests.cs`, using repo-local test files under `AppContext.BaseDirectory` instead of temp directories.

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

### 2026-05-28: Twitch Auth + Integration Connection Monitor

**Work completed:**
- Added `IIntegrationConnectionMonitor` to `src\Thiccdal.Infrastructure\Integrations\` — generic interface for any platform's connection state. Inara can inject `IEnumerable<IIntegrationConnectionMonitor>` to render all platform statuses reusably.
- Added `ITwitchConnectionMonitor : IIntegrationConnectionMonitor` to `src\Thiccdal.Infrastructure\Twitch\` — typed interface for Twitch-specific injection.
- Added `TwitchConnectionMonitor` to `src\Remote\Thiccdal.Remote.Twitch\` — singleton; checks DB for a non-expired token; raises `ConnectionChanged` event when state flips. Blazor components subscribe and call `InvokeAsync(StateHasChanged)`.
- Updated `ChatBotRegistrationExtension.AddChatBotServices()` to register `TwitchConnectionMonitor` as a shared singleton exposed as both `ITwitchConnectionMonitor` and `IIntegrationConnectionMonitor`.
- Updated `/auth/twitch/callback` in `Program.cs` to inject `ITwitchConnectionMonitor` and call `RefreshConnectionState` after `StoreToken` — Blazor circuits are notified before redirect.
- Added 10 real tests (all passing): 4 for `TwitchTokenManager`, 6 for `TwitchConnectionMonitor`.

**Key DI pattern — shared singleton across typed/generic interfaces:**
```csharp
collection.AddSingleton<TwitchConnectionMonitor>();
collection.AddSingleton<ITwitchConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
collection.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
```
Use this pattern for every new platform so the same instance is reachable both ways.

**Key files:**
- `src\Thiccdal.Infrastructure\Integrations\IIntegrationConnectionMonitor.cs`
- `src\Thiccdal.Infrastructure\Twitch\ITwitchConnectionMonitor.cs`
- `src\Remote\Thiccdal.Remote.Twitch\TwitchConnectionMonitor.cs`
- `src\Modules\Thiccdal.Modules.ChatBot\ChatBotRegistrationExtension.cs`
- `src\Thiccdal\Program.cs`
- `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TestProject1\TwitchConnectionMonitorTests.cs`
- `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TestProject1\TwitchTokenManagerTests.cs`

**OAuth scopes note:** Current `GetAuthorizationUrl` now requests Phase 17 scopes: `user:read:chat user:write:chat chat:read chat:edit moderator:read:followers`.

### 2026-05-29: Batch Completion — Twitch Auth + Integration Surface

**Team summary:**
- Inara built reusable `IntegrationConnector` + `IntegrationAuthDialog` components (generic, platform-agnostic)
- River implemented `ITwitchService` state machine; owns connection state truth
- Kaylee's `IIntegrationConnectionMonitor` pattern enables platform enumeration; complementary to River's service-level state
- Jayne secured the OAuth flow and token management; 22 tests passing
- Mal confirmed integration and no architectural issues

**Key patterns confirmed for next platforms:**
- DI registration: `AddSingleton<T>()` then forward to `IInterface1` and `IInterface2` via `sp.GetRequiredService<T>()`
- Connection state lives in the service (River's layer); monitor is DB-only check for status queries
- Event-driven Blazor updates: components subscribe to state change events, call `InvokeAsync(StateHasChanged)`

**Status:** ✅ 10 tests from Kaylee's work passing (integration monitor + token manager). Infrastructure ready for Phase 17.

### 2026-05-28: OpenTelemetry CVE Remediation

- The Aspire/OpenTelemetry package versions are managed directly in `src\Aspire\Thiccdal.Aspire.ServiceDefaults\Thiccdal.Aspire.ServiceDefaults.csproj`; there is no central `Directory.Packages.props` file in this repo.
- For OpenTelemetry package families, do not assume every sibling package ships the same latest patch version. Verify each package's exact published version before editing or restore can fail with `NU1102`.
- The host build CVE failure was cleared by moving off `1.14.0` to the latest available safe versions per package: OTLP exporter and hosting `1.15.3`, AspNetCore instrumentation `1.15.2`, Http/runtime instrumentation `1.15.1`.

### 2026-05-26: Twitch Test Project Structure Correction

- The Twitch test project belongs at `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj`; an extra template-named child folder breaks the repo's solution-to-disk mirroring rule.
- When moving a nested test project up to its solution-matching folder, update both the `.slnx` project path and any relative `ProjectReference` hops; the move changes `..\` depth even when code stays the same.
- Validation for this fix passed with solution restore/build plus targeted test execution for `Thiccdal.Remote.Twitch.Tests`.

### 2026-05-29: Stored Token Click Path Fix

**Problem:** TopBar TWI chip click did nothing when operator already had a stored Twitch token.

**Root cause:** Incomplete edits in `TopBar.razor` referencing undefined `_twitchIsAuthorized` field prevented compilation. The click handler (`OpenTwitchConnect`) correctly navigates to `/twitch/connect`, but the component never compiled.

**Fix:** Removed broken `_twitchIsAuthorized` field references from `OnInitializedAsync` (line 64) and `OnTwitchStateChanged` (line 72). The navigation-based flow already handles all token states:
- `NotAuthorized` → shows "Authorize with Twitch" button
- `Authorized` → shows "Connect to IRC" button
- `Connected` → shows "Disconnect" button

**Key pattern confirmed:** Control module uses page navigation (`/twitch/connect`) for full auth/connection management, not modal dialogs. The `IntegrationConnector` chip is always clickable when `OnConnectClicked` has a delegate; state determines what the target page shows.

**Testing:** 27 tests passing (25 Twitch, 1 Data, 1 host). Clean build confirmed no compilation errors.

- 2026-05-29: For Helix/EventSub groundwork, keep Twitch config typed and transport-aware: `TwitchOptions` now owns `BotUserId`, `OAuthBaseAddress`, `Helix`, and `EventSub` sub-options so host code and adapters share one validated shape.
- 2026-05-29: Split Twitch remote HTTP boundaries with named clients (`Twitch.OAuth`, `Twitch.Helix`) in `src\Remote\Thiccdal.Remote.Twitch\TwitchRegistrationExtensions.cs`; bind/validate them there, then keep `Program.cs` at composition-only level.
- 2026-05-29: `TwitchChatConnectionProfile` must carry both `BotUserId` and `BroadcasterId`; EventSub subscriptions need the authenticated bot identity and target broadcaster identity separately even after the UI-selected target channel override.
- 2026-05-29: Helix contract coverage currently lives in `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchRegistrationExtensionsTests.cs`, `TwitchTargetChannelServiceTests.cs`, and `TwitchServiceTests.cs`; validated with `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj` plus `dotnet build src\Thiccdal\Thiccdal.csproj`.

### 2026-05-29: Helix Contract Groundwork — Typed Options and Identity Separation

**Requested by:** Squad coordination (River needs stable seam before implementing ITwitchHelixClient)

**What landed:**
- `TwitchOptions` expanded to carry `BotUserId`, `OAuthBaseAddress`, and dedicated `Helix` + `EventSub` sub-options
- `TwitchChatConnectionProfile` now includes both `BotUserId` and `BroadcasterId` (required for separate EventSub identities)
- `AddTwitchIntegration()` moved to own all OAuth, Helix, and EventSub validation + named HttpClient setup (`Twitch.OAuth` vs `Twitch.Helix`)

**Why this matters:**
- EventSub subscription APIs need authenticated bot user ID and broadcaster ID independently to set up stream topic subscriptions correctly
- Host and UI code read one stable, typed config shape instead of inheriting adapter-internal constants or hard-coded strings
- River's ITwitchHelixClient work can proceed without re-laying DI boundaries or moving auth setup again

**Key Files:**
- `src\Thiccdal.Infrastructure\Twitch\TwitchOptions.cs` (updated)
- `src\Thiccdal.Infrastructure\Twitch\TwitchHelixOptions.cs` (new)
- `src\Thiccdal.Infrastructure\Twitch\TwitchEventSubOptions.cs` (new)
- `src\Thiccdal.Infrastructure\Twitch\TwitchChatConnectionProfile.cs` (updated)
- `src\Remote\Thiccdal.Remote.Twitch\TwitchRegistrationExtensions.cs` (updated)

**Tests:** ✅ Host build, ✅ Twitch adapter tests

**Next:** River can now implement ITwitchHelixClient without worrying about option shape or DI changes.
- 2026-05-29: For Blazor route surfaces, keep the Router and `MapRazorComponents<App>()` in sync via a shared route-assembly catalog (`src\Thiccdal\RouteAssemblyCatalog.cs`); otherwise module pages can hydrate differently from direct requests.
- 2026-05-29: Routable Razor pages should own their own `CancellationTokenSource` field instead of injecting one from DI. `src\Thiccdal\Components\Pages\TwitchConnect.razor` now uses a private `_cts`, which prevents route activation failures when navigating from the dashboard chip.
- 2026-05-29: Host route smoke coverage now lives in `src\Tests\Thiccdal.Tests\RouteRenderingTests.cs` with `ThiccdalApplicationFactory`; use WebApplicationFactory + repo-local SQLite config overrides to prove `/dashboard` and `/twitch/connect` both render.
- 2026-05-29: Use a singleton `IActivityFeedService` plus `PlatformActivityFormatter` to centralize chat/event rendering for `/prompter` and `/overlay`; this avoids each page reformatting Twitch follows, raids, cheers, badges, and emotes independently.
- 2026-05-29: When a Blazor surface needs background event history, register the same singleton as both its app-facing interface and `IHostedService` so subscriptions are active before the bot connection starts (`src\Modules\Thiccdal.Modules.ChatBot\Services\ActivityFeedService.cs`).
- 2026-05-29: Rich Twitch rendering now relies on normalized `ChatMessagePart` + `ChatBadge` data from `src\Remote\Thiccdal.Remote.Twitch\TwitchEventSubNotificationMapper.cs`; downstream UI should prefer those contracts over reparsing raw payload text.
