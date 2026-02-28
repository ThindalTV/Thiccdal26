# Copilot Instructions – Thiccdal

## Project Overview

Thiccdal is a **streaming command and control system** built with .NET 10 and Blazor Server.
It runs on one machine (stream PC) and is controlled from another (e.g., a Surface Pro tablet).
It handles multicast RTMP streaming, multi-platform chat aggregation, a chatbot, a live overlay,
a teleprompter, event tracking (follows, subs, redeems, etc.) and stream recording.

---

## Tech Stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor Server |
| Database | SQLite (production), InMemory EF Core (unit tests), SQLite (integration tests) |
| ORM | Entity Framework Core (direct use, no repository wrapper unless justified) |
| Orchestration | .NET Aspire |
| Configuration | `IOptions<T>` pattern — no magic strings for settings |
| Logging | `ILogger<T>` — structured logging throughout |
| Testing | xUnit, with InMemory DB for unit tests and SQLite for integration tests |

---

## Solution & Directory Layout

```
/src/Thiccdal/                    Blazor Server host
/src/Thiccdal.Infrastructure/     Interfaces, enums, value types — NO EF Core here
/src/Thiccdal.Data/               EF Core DbContext, entity models, migrations
/src/Thiccdal.Remote/
  Thiccdal.Remote.Twitch/         Twitch platform integration
  Thiccdal.Remote.YouTube/        YouTube platform integration
  Thiccdal.Remote.Facebook/       Facebook Live integration
  Thiccdal.Remote.X/              X (Twitter) Live integration
  Thiccdal.Remote.Discord/        Discord platform integration
  Thiccdal.Remote.LinkedIn/       LinkedIn integration (disabled until API approved)
  Thiccdal.Remote.TikTok/         TikTok Live (disabled until API approved)
/src/Thiccdal.Streaming/          RTMP ingest, fanout, recording
/src/Thiccdal.Overlay/            Overlay Blazor components/hub
/src/Aspire/AppHost/              Aspire AppHost
/src/Aspire/ServiceDefaults/      Aspire ServiceDefaults
/docs/architecture/               Architecture .md files
/docs/help/                       End-user documentation
/architecture/                    Architectural decision records (ADRs)
```

Solution file: `Thiccdal.slnx` at repo root.
**Directory structure on disk must mirror the solution structure.**

---

## Key Patterns & Conventions

### IOptions Configuration
All configuration is accessed through typed `IOptions<T>` classes — never `IConfiguration["key"]` directly.

```csharp
// Good
public class TwitchOptions
{
    public string Channel { get; set; } = string.Empty;
    public string OAuthToken { get; set; } = string.Empty;
}
// Inject: IOptions<TwitchOptions> or IOptionsSnapshot<TwitchOptions>
```

### Platform Abstraction
Every streaming/chat target implements `IPlatformConnection`, defined in `Thiccdal.Infrastructure`.
Implementations are registered in DI and never referenced by concrete type outside their own project.
The `Null` implementation logs all operations and returns no-op results — use it as the default in unit tests.

### Event System
All platform happenings derive from `PlatformEvent`. Recognized events (e.g. `TwitchSubscribeEvent`)
carry extra detail. Unrecognized events emit the base `PlatformEvent` with a `RawData` string.
Events are always persisted before being dispatched.

### Entity Framework
- Entity models and `ApplicationDbContext` live in **`Thiccdal.Data`**.
- Interfaces and shared value types live in **`Thiccdal.Infrastructure`**.
- Platform adapters and the Blazor host reference `Thiccdal.Infrastructure`; only `Thiccdal.Data` references `Thiccdal.Infrastructure`.
- Use `ApplicationDbContext` directly — no generic repository wrapper.
- SQLite for production; `UseInMemoryDatabase` for unit tests; SQLite for integration tests.
- Migrations live in `Thiccdal.Data`.

### Blazor Server
- Realtime updates via SignalR (built-in Blazor circuit).
- Push state changes using `InvokeAsync(StateHasChanged)` from background services.
- Touch-friendly UI — primary control device is a Surface Pro tablet.

### Null Platform
`Thiccdal.Remote.Null` provides no-op implementations of all platform interfaces.
It must log every operation at `Information` level so test output is inspectable.

---

## Code Style

- File-scoped namespaces everywhere.
- `private` by default; only widen access when required.
- The entire codebase is async — do **NOT** add an `Async` suffix to any method name.
- Every public method accepts and forwards a `CancellationToken`.
- All services and significant logic-bearing types must have an interface defined in `Thiccdal.Infrastructure/Interfaces/`. Register and consume via DI; never instantiate directly.
- Prefer records for DTOs; classes for services/entities.
- No comments explaining *what* code does — comments explain *why*.
- No unused parameters, no dead code, no TODO comments in committed code.
- Warnings are treated as errors solution-wide — fix every warning before committing.

---

## Testing Conventions

- Test project per source project: `Thiccdal.Infrastructure.Tests`, etc.
- Mirror source class names: `ChatService` → `ChatServiceTests`.
- Method naming: `WhenUserSubscribes_ThenSubscribeEventIsPersisted`.
- AAA layout; one behavior per `[Fact]`; `[Theory]` + `[InlineData]` for parameterized cases.
- Never mock internal code. Only mock external I/O (platform APIs, file system, clock).
- Integration tests spin up a real SQLite file in a temp directory; path is logged but not cleaned up.

---

## Copilot Prompt Patterns

When asking Copilot to scaffold something, provide the following context:

### Adding a new platform
```
Using #file:src/Thiccdal.Infrastructure/Interfaces/IPlatformConnection.cs as the contract,
scaffold a new project Thiccdal.Remote.<Platform> that implements IPlatformConnection,
IChatService, and IStreamTarget. Register it via IOptions<<Platform>Options>.
Include a xUnit test class with a Null platform standing in for the live API.
```

### Adding a new event type
```
Using #file:src/Thiccdal.Infrastructure/Events/PlatformEvent.cs as the base,
add a new <Platform><Name>Event record. Add it to the event mapping in
#file:src/Thiccdal.Remote.<Platform>/EventMapper.cs. Write a [Fact] test covering the mapping.
```

### Adding a new overlay component
```
Using the pluggable overlay pattern in #file:src/Thiccdal.Overlay/IOverlayComponent.cs,
create a new Blazor component <Name>OverlayComponent.razor that implements the interface.
Register it in the overlay pipeline. Write a Bunit test covering render output.
```

### Adding a new chatbot command handler
```
Using #file:src/Thiccdal.Infrastructure/Interfaces/ICommandHandler.cs as the contract,
scaffold a new <Name>CommandHandler that is wired to a BotCommand row via its HandlerType column.
Inject dependencies via DI. Write two [Fact] tests: happy-path and error/edge-case.
```

---

## What NOT to do

- Do not access `IConfiguration` by magic string — use `IOptions<T>`.
- Do not skip defining an interface — every service must have one in `Thiccdal.Infrastructure/Interfaces/`.
- Do not add the `Async` suffix to any method — the whole codebase is async by convention.
- Do not use `async void` except in Blazor event handlers.
- Do not reference a concrete service type outside its own project — always inject the interface.
- Do not hard-code platform credentials — they live in `appsettings.json` / environment variables.
- Do not modify files under `/obj/` or `*.g.cs`.
- Do not add `public` to a type or member without a clear reason.
- Do not leave warnings unresolved — `TreatWarningsAsErrors` is on solution-wide.
