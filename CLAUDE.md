# Thiccdal

Streaming command-and-control system, .NET 10 + Blazor Server. It runs on the stream PC and is
operated from a separate device (typically a Surface Pro tablet in a browser). It handles RTMP
ingest and fanout, multi-platform chat aggregation, a chatbot, a live overlay, a teleprompter,
event tracking (follows, subs, redeems), and stream recording.

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
src/Thiccdal.Streaming/             Streaming services consumed by the host
src/Thiccdal.RtmpServer/            Standalone RTMP ingest/fanout/recording server
src/Modules/Thiccdal.Modules.*/     ChatBot, Control, Overlay, Teleprompter
src/Remote/Thiccdal.Remote.*/       Per-platform adapters (Twitch, YouTube, Discord, Facebook,
                                    X, LinkedIn, TikTok, Instagram, LMStudio, Null)
src/Shared/Thiccdal.Shared.Components/
src/Aspire/                         AppHost, ServiceDefaults
src/Tools/                          Teleprompter.Display (Windows-only, built separately)
src/Tests/                          Mirrors the source tree: Tests/, Tests/Modules/, Tests/Remote/
docs/architecture/                  Architecture docs
docs/help/                          End-user documentation
architecture/                       Architectural decision records
```

Interfaces live in `Thiccdal.Infrastructure/<Domain>/` — grouped by domain (`Remotes/`, `Bot/`,
`Streaming/`, `Overlay/`, `Setup/`, …), **not** in a flat `Interfaces/` folder.

Key contracts:
- `src/Thiccdal.Infrastructure/Remotes/IPlatformConnection.cs`
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

- `docs/architecture/overview.md` still describes `Thiccdal.Streaming` as the RTMP ingest/fanout
  host; that moved to `Thiccdal.RtmpServer`.
- Stray local `artifacts/` folders are gitignored but were being globbed into compilation;
  `Directory.Build.props` now excludes them via `DefaultItemExcludes`.
