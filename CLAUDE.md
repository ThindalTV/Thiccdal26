# Thiccdal

Streaming command-and-control system, .NET 10 + Blazor Server. It runs on the stream PC and is
operated from a separate device (typically a Surface Pro tablet in a browser). It handles
multi-platform chat aggregation, a chatbot, a live overlay, a teleprompter, event tracking
(follows, subs, redeems), and a pre-live checklist with a go-live action.

Twitch is the only platform. The adapter architecture stays modular (`IPlatformConnection`, one
project per platform under `src/Remote/`) so others can be added later, but do not add references
to YouTube, Discord, Facebook, X, or any other platform.

Video is out of scope: Thiccdal never ingests, restreams, or records video — OBS publishes to
Twitch directly. Do not reintroduce RTMP ingest, fanout, relay, or disk recording.

Every surface is a web page — there is no companion desktop application. The teleprompter is
displayed as an OBS custom browser dock pointed at `http://<host>/prompter`. Because the OBS
browser engine cannot be taught to trust the dev certificate, the host applies **no HTTPS
redirect and no HSTS**; every surface must stay reachable over plain HTTP. Do not add
`UseHttpsRedirection` or `UseHsts` back to `Program.cs`, and do not reintroduce a downloadable
prompter executable.

## Surfaces

Four separate surfaces, each with its own layout and input model:

| Surface | Route | Input | Purpose |
|---|---|---|---|
| Streamer dashboard | `/dashboard` | Touch | Instant control while live, single mode. **No setup lives here.** |
| Teleprompter | `/prompter` | Touch / read-only | On-camera script and chat |
| Overlay | `/overlay` | — | OBS browser source |
| Configuration | `/config` | Keyboard + mouse, large screen | Everything else |

`/config` has two sections: **Bot** (commands, autoresponses, identity and greetings) and
**System** (Twitch, AI keys, AI memory, viewer identities, overlay cards, sponsorship, pre-live
checklist, appearance).

The dashboard is three columns: teleprompter controls and predefined overlay cards on the left,
the question queue over a lower-third preview in the middle, and one-tap bot commands on the
right. Bot commands carry effects — send in chat, show on lower third, or both — and the
lower-third slot is owned by `ILowerThirdService`, so a promoted question and operator copy never
share the screen.

There is no setup wizard — `/config` is the single configuration surface, and `/` redirects to it.

Adding an editing affordance to the dashboard or teleprompter is a mistake; it belongs in
`/config`. Components shared between the two get an `Inline` parameter that drops the modal
chrome (see `BotCommandManagementDialog`, `PersonalPrepManageDialog`).

### Readiness gating

`ISystemReadinessService` (`Thiccdal.Infrastructure/Readiness/`) reports what is configured.
Gated surfaces wrap themselves in `<ReadinessGate>`:

- Teleprompter needs a saved Twitch channel.
- Streamer dashboard needs a saved channel **and** an authorized Twitch account.

Until then each shows an unconfigured notice pointing at `/config`, and activates automatically
once the requirement is met.

## Build and test

```bash
dotnet build Thiccdal.slnx
```

```bash
dotnet test Thiccdal.slnx
```

Solution file is `Thiccdal.slnx` (XML solution format) at the repo root.

`TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on solution-wide — a warning fails the
build. Fix them rather than suppressing.

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor Server |
| Database | SQLite via EF Core; `UseInMemoryDatabase` for unit tests |
| ORM | EF Core used directly — no generic repository wrapper |
| Orchestration | .NET Aspire |
| Configuration | `IOptions<T>` — never `IConfiguration["key"]` |
| Logging | `ILogger<T>`, structured |
| Testing | xUnit (+ Moq / NSubstitute where already used) |

## Layout

Directory structure on disk mirrors the solution structure.

```
src/Thiccdal/                       Blazor Server host (pages, setup wizard, layouts)
src/Thiccdal.Infrastructure/        Interfaces, options, enums, value types — NO EF Core here
src/Thiccdal.Data/                  DbContext, entity models, migrations
src/Thiccdal.API/                   Minimal API endpoint extensions
src/Thiccdal.AI/                    AI/LLM services
src/Modules/Thiccdal.Modules.*/     ChatBot, Control, Overlay, Teleprompter
src/Remote/Thiccdal.Remote.*/       Per-platform adapters (Twitch, Obs, LMStudio, Null)
src/Shared/Thiccdal.Shared.Components/
src/Aspire/                         AppHost, ServiceDefaults
src/Tests/                          Mirrors the source tree: Tests/, Tests/Modules/, Tests/Remote/
docs/architecture/                  Architecture docs
docs/help/                          End-user documentation
architecture/                       Architectural decision records
```

Interfaces live in `Thiccdal.Infrastructure/<Domain>/` — grouped by domain (`Remotes/`, `Bot/`,
`Streaming/`, `Overlay/`, `Setup/`, …), **not** in a flat `Interfaces/` folder.

Key contracts:
- `src/Thiccdal.Infrastructure/Remotes/IPlatformConnection.cs`
- `src/Thiccdal.Infrastructure/Streaming/IObsConnection.cs`
- `src/Thiccdal.Infrastructure/Remotes/IChatSource.cs`
- `src/Thiccdal.Infrastructure/Bot/ICommandDispatcher.cs`
- `src/Thiccdal.Infrastructure/Overlay/IOverlayComponent.cs`
- `src/Thiccdal.Data/Models/PlatformEvent.cs`

## Code style

- File-scoped namespaces.
- `private` by default; widen only when required.
- **No `Async` suffix on method names** — the whole codebase is async by convention.
- Every public method takes and forwards a `CancellationToken`.
- No `async void` except in Blazor event handlers.
- Services and logic-bearing types get an interface in `Thiccdal.Infrastructure`; register and
  consume via DI, never instantiate directly.
- Records for DTOs, classes for services and entities.
- **Do not use primary constructors (IDE0290) or target-typed `new(...)` (IDE0090).** Both are
  suppressed in `.editorconfig` — project style is explicit constructors and explicit type names.
  `IDE0305` is likewise suppressed.
- Comments explain *why*, never *what*.
- No dead code, unused parameters, or committed TODOs.

## Patterns

### Configuration
All config goes through typed `IOptions<T>` classes. Never read `IConfiguration` by magic string.

Configuration is mid-migration from `appsettings.json` toward database-backed settings: there is
an `AppConfiguration` key/value table and an `IConfigurationPersistenceService`
(`Thiccdal.Infrastructure/Setup/`) with typed JSON get/set, used by the installation wizard.
Most `*Options` classes still bind from `appsettings.json`.

### Platform abstraction
Every chat/streaming target implements `IPlatformConnection`. Implementations are resolved through
DI and never referenced by concrete type outside their own project. `Thiccdal.Remote.Null` provides
no-op implementations that log every operation at `Information` level — use it as the default in tests.

### Events
Platform happenings derive from `PlatformEvent`. Recognised events (e.g. `TwitchSubscribeEvent`)
carry extra detail; unrecognised ones emit the base type with a `RawData` string. Events are
persisted before dispatch.

### Entity Framework
- Entities and `ApplicationDbContext` in `Thiccdal.Data`; interfaces and value types in
  `Thiccdal.Infrastructure`. Only `Thiccdal.Data` references `Thiccdal.Infrastructure` in that
  direction — adapters and the host depend on `Thiccdal.Infrastructure`.
- Use `ApplicationDbContext` directly. Migrations live in `Thiccdal.Data/Migrations`.

### Blazor Server
- Realtime updates ride the built-in SignalR circuit.
- Push state changes from background services via `InvokeAsync(StateHasChanged)`.
- Touch-friendly UI — the primary control device is a tablet.
- No third-party CSS libraries; isolated `.razor.css` per component.

## Testing

- Test project per source project, mirroring the source tree.
- Mirror class names: `ChatService` → `ChatServiceTests`.
- Method naming: `WhenUserSubscribes_ThenSubscribeEventIsPersisted`.
- AAA layout, one behaviour per `[Fact]`, `[Theory]` + `[InlineData]` for parameterised cases.
- Only mock external I/O (platform APIs, filesystem, clock). Never mock internal code.
- **Logic tests only — no bUnit, no `WebApplicationFactory`.** Component rendering and HTTP
  transport tests were deliberately removed; do not reintroduce those dependencies.
- Update tests in the same commit as the API change that breaks them.

## Secrets

Never commit credentials, tokens, or `.env` contents. Platform credentials come from
`appsettings.json` (gitignored local overrides), user secrets, or environment variables — and
increasingly from the database-backed settings store. Never paste live secret values into
committed files, logs, commit messages, or documentation; refer to the config key instead.

A test project that is not listed in `Thiccdal.slnx` is never built or run. Eight orphaned ones
were removed for this reason — if you add a test project, add it to the solution in the same
commit.

## Known rough edges

- Stray local `artifacts/` folders are gitignored but were being globbed into compilation;
  `Directory.Build.props` now excludes them via `DefaultItemExcludes`.
