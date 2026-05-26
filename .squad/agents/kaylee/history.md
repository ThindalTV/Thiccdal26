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
