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
- 2026-05-28: Phase 8 restream backend now persists operator restream settings and destination toggles via EF Core (`RestreamConfiguration`, `RestreamDestinationConfiguration`) and exposes them through `IRestreamRuntimeService` + `/api/restream/*`.
- 2026-05-28: The Null integration must register an `IPlatformManualReminderProvider`; otherwise host-backed tests that swap in only `AddNullIntegration()` fail when `PreLiveChecklistService` is activated.
- 2026-05-28: After schema changes, clean repo-local SQLite test databases under `src\Tests\**\bin\**\*.db*` before rerunning the full suite; reused files can surface false migration failures like `table "RestreamConfigurations" already exists`.

### 2026-05-28–2026-05-29: Helix Infrastructure & Auth Groundwork (Archived)

Helix redesign data model locked: ChatFragment hierarchy (TextFragment, EmoteFragment, CheermoteFragment), ChatEvent gains Fragments/Color/Badges. New PlatformEvent subtypes: TwitchFollowEvent, TwitchSubscribeEvent, TwitchCheerEvent, TwitchRaidEvent, TwitchRedeemEvent. All events persisted before dispatch. Twitch auth + integration connection monitor completed: `IIntegrationConnectionMonitor` + `ITwitchConnectionMonitor` pattern enables platform enumeration. DI pattern: `AddSingleton<T>()` forwarded to both typed/generic interfaces. TwitchOptions expanded: `BotUserId`, `OAuthBaseAddress`, `Helix`, `EventSub` sub-options. TwitchChatConnectionProfile includes `BotUserId` + `BroadcasterId` for separate EventSub identities. 10 tests passing on monitor/token work.

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

## Phase 8 Restream (2026-05-28)

✅ Kaylee restored the restream backend control-plane with full config persistence and test coverage. Rebuilt `IRestreamRuntimeService` control surface with clean API contracts. Persisted operator restream settings and destination toggles via EF Core (`RestreamConfiguration`, `RestreamDestinationConfiguration` entities). Re-established config migration path for database schema changes and validated full test suite passing.

**Key decisions locked for restream control-plane:**
- UI consumers use `RestreamControlState` and `RestreamConfigurationUpdateRequest` DTOs (not data-layer entities)
- Operator-facing contract stays stable while backend independently persists runtime choices and platform adapter secrets
- Startup automation flows through existing `RestreamBootstrapService` + host DB migration (no new patterns required)
- `/api/restream/*` endpoints provide clean boundary: state, config, toggle, start, stop

**Cross-team integration points:**
- River's adapter layer provides capability discovery and event propagation to backend service
- Inara's UI pattern (two-entry: pre-live settings + live toolbar) consumes these API endpoints
- All three slices tested and building cleanly; no blocking dependencies
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

### 2026-05-28: Issue #129 — Chat Display Name Canonicalization (Backend Persistence Seam)

**Problem:** Display name canonicalization was split across event mapping and UI layer, causing inconsistent viewer-name merges when the same user appeared with different name casing/formatting.

**Solution implemented:**
- Centralized canonical display-name normalization in `TwitchEventSubNotificationMapper.cs` (single source of truth)
- `ChatEvent.PreferredAuthor` and `DisplayAuthor` now carry normalized names set at persistence time
- Raw platform `Author` preserved separately for bot logic that keys off source-native data
- Activity-feed formatter and downstream UI components consume `DisplayAuthor` for rendering
- EF Core migration generated and validated; SQLite schema updated forward-compatible

**Integration point:** Upstream of Inara's UI render fix in `PrompterLine.razor` and `ChatView.razor`. Backend now produces canonical display names at event-mapping time; UI layer reads and renders those.

**Validation:** ✅ `dotnet test .\\Thiccdal.slnx` (all tests passing), ✅ SQLite integration tests confirm migration and persistence work correctly

**Status:** ✅ Backend seam complete. Inara's UI render fix now properly consumes canonical names.

**Key files modified:**
- `src/Remote/Thiccdal.Remote.Twitch/TwitchEventSubNotificationMapper.cs` (centralized normalization)
- `src/Thiccdal.Data/Models/ChatEvent.cs` (PreferredAuthor/DisplayAuthor added)
- `src/Thiccdal.Data/Models/PlatformEvent.cs` (schema updated)
- `src/Thiccdal.Data/Migrations/...` (auto-generated schema migrations)
- 2026-05-28: Phase 8 recording persistence lives behind `IStreamRecordingService` in `src\Thiccdal.Infrastructure\Streaming\IStreamRecordingService.cs`, implemented by `src\Thiccdal.Data\StreamRecordingService.cs`; keep the EF entity in `src\Thiccdal.Data\Models\StreamRecording.cs` and expose API state through infrastructure snapshots, not EF models.
- 2026-05-28: Start `StreamRecording` rows only when recording is actually armed or started, not when the operator merely toggles restream state. `src\Thiccdal.Streaming\StreamingService.cs` waits for ingest-listener lifecycle signals before delegating to `src\Thiccdal.Streaming\DiskRecorder.cs`, so `RestreamControlState.LatestRecording` stays null until OBS/ingest really goes live.
- 2026-05-28: The recording foundation now uses `IRecordingProcessRunner` + `IDiskRecorder` in `src\Thiccdal.Infrastructure\Streaming\`, with the FFmpeg runner in `src\Thiccdal.Streaming\FfmpegRecordingProcessRunner.cs`; validate the persistence seam with `dotnet test .\src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj` and the streaming library with `dotnet build .\src\Thiccdal.Streaming\Thiccdal.Streaming.csproj --no-restore`.

### 2026-05-31: Phase 8 Recording Persistence — Completed

**Shipped:** Full recording persistence layer with honest ingest-driven state management.

- **StreamRecording entity:** Lifecycle tracking (pending → recording → stopped → failed) with EF Core schema
- **IStreamRecordingService:** Infrastructure interface; Data layer implementation
- **DiskRecorder & FfmpegRecordingProcessRunner:** FFmpeg process orchestration in Streaming library
- **RestreamRuntimeService updated:** Only reports latest recording after ingest listener transitions live AND DiskRecorder creates a row

**Key Design:** Recording state is operator-awareness state, not operator-intent. Preserves honest streaming state visibility while River completes the ingest/media path.

**Integration Remaining:** River's real RTMP ingest must become FFmpeg input source. Currently local capture follows listener lifecycle; should follow actual multicast stream data.

**Validation:** All Phase 8 recording tests passing; integration paths verified via RestreamRuntimeService contract.
