---
name: database-migrations
description: EF Core and SQLite conventions for this repo — startup migration of a missing database, IDbContextFactory usage from singletons, and adding migrations. Use when changing entity models, adding a migration, or touching database startup.
---

# Database and migrations

SQLite via EF Core. `ApplicationDbContext`, entities, and migrations all live in
`src/Thiccdal.Data/`. Unit tests use `UseInMemoryDatabase`.

## Startup initialisation

Operators may delete `thiccdal.db` and expect the app to recreate it on next launch. The host
migrates on startup rather than assuming the file exists:

```csharp
public static async Task InitializeDatabase(
    this IServiceProvider services,
    CancellationToken cancellationToken = default)
{
    using IServiceScope scope = services.CreateScope();
    IDbContextFactory<ApplicationDbContext> factory =
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

    await using ApplicationDbContext dbContext = await factory.CreateDbContextAsync(cancellationToken);
    EnsureSqliteDirectoryExists(dbContext);
    await dbContext.Database.MigrateAsync(cancellationToken);
}
```

Wired as:

```csharp
var app = builder.Build();
await app.Services.InitializeDatabase(app.Lifetime.ApplicationStopping);
```

Three things matter here:

- **`MigrateAsync()`, never `EnsureCreated()`.** `EnsureCreated` skips migration history and
  leaves a database the migration pipeline cannot evolve.
- **Create the parent directory first.** SQLite will not create a missing folder and fails with
  "unable to open database file".
- **`IDbContextFactory`, not `ApplicationDbContext`.** The context is registered with
  `AddDbContextFactory`, and background services, connection monitors, and other singletons all
  consume it through the factory. Resolving the context directly from a singleton will fail or
  leak a scoped instance.

Covered by `ApplicationDbContextInitializationExtensionsTests`.

## Adding a migration

Run from the repo root, targeting the data project with the host as startup project:

```bash
dotnet ef migrations add <Name> --project src/Thiccdal.Data --startup-project src/Thiccdal
```

Commit the migration, its `.Designer.cs`, and the updated `ApplicationDbContextModelSnapshot.cs`
together — a snapshot that drifts from the migrations produces confusing phantom diffs on the next
`migrations add`.

## Conventions

- Use `ApplicationDbContext` directly; there is no generic repository wrapper and adding one needs
  justification.
- Entities live in `src/Thiccdal.Data/Models/`. Interfaces and value types stay in
  `Thiccdal.Infrastructure` — `Thiccdal.Data` depends on Infrastructure, never the reverse.
- Adapters under `src/Remote/` must not reference `Thiccdal.Data`; they persist through
  Infrastructure-owned seams. See the `platform-adapter` skill.

## Lifecycle rows reflect reality

When persisting the lifecycle of an external process (for example stream recording), create the
row at **start intent** and complete it on both clean and failed shutdown. A row written only on
success silently loses every crash, which is exactly the case an operator needs to see afterwards.

## Configuration is migrating into the database

There is an `AppConfiguration` key/value table with `IConfigurationPersistenceService`
(`Thiccdal.Infrastructure/Setup/`) providing typed JSON get/set, used by the installation wizard.
Settings are moving here from `appsettings.json`. Anything stored there may include secrets — see
the `secret-handling` skill before logging or surfacing values.
