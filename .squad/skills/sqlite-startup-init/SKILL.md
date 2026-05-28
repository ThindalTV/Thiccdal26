---
name: "sqlite-startup-init"
description: "Initialize a missing SQLite database at app startup by applying EF Core migrations through IDbContextFactory."
domain: "ef-core"
confidence: "high"
source: "earned"
---

## Context

Use when a .NET app stores data in SQLite and operators may delete the database file, expecting the app to recreate it on next launch.

## Pattern

1. Register the context with `AddDbContextFactory<ApplicationDbContext>(...)`.
2. After building the app, call a startup initializer before mapping endpoints or serving requests.
3. Inside the initializer:
   - create a scope
   - resolve `IDbContextFactory<ApplicationDbContext>`
   - create the context with the provided `CancellationToken`
   - parse the SQLite connection string and create the parent directory if needed
   - call `Database.MigrateAsync()` (not `EnsureCreated`)

```csharp
public static async Task InitializeDatabase(this IServiceProvider services, CancellationToken cancellationToken = default)
{
    using IServiceScope scope = services.CreateScope();
    IDbContextFactory<ApplicationDbContext> factory =
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

    await using ApplicationDbContext dbContext = await factory.CreateDbContextAsync(cancellationToken);
    EnsureSqliteDirectoryExists(dbContext);
    await dbContext.Database.MigrateAsync(cancellationToken);
}
```

Startup wiring:

```csharp
var app = builder.Build();
await app.Services.InitializeDatabase(app.Lifetime.ApplicationStopping);
```

## Why this pattern

- `MigrateAsync()` preserves the migration history and recreates the database from the authoritative EF schema.
- `IDbContextFactory<ApplicationDbContext>` matches singleton-safe consumption patterns already used by background and integration services.
- Explicit directory creation prevents SQLite "unable to open database file" failures when the configured path includes a missing folder.

## Tests to include

- Missing database file gets recreated and has the expected applied migration.
- Missing parent directory is created automatically before migration.

## Anti-patterns

- Using `EnsureCreated()` for a migrated production database
- Resolving `ApplicationDbContext` directly when only `AddDbContextFactory` is registered
- Assuming SQLite will create missing parent directories on its own
