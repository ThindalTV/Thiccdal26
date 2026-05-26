---
name: "integration-connection-monitor"
description: "Per-platform OAuth connection state observable from Blazor Server via a singleton event-raising service."
domain: "integrations"
confidence: "high"
source: "earned"
---

## Context

Use when a platform integration needs to expose "is authenticated?" state to the Blazor admin UI, and the UI must update automatically when the state changes (e.g., after an OAuth callback).

## Pattern

### 1. Infrastructure contracts

Define a generic interface in `Thiccdal.Infrastructure.Integrations`:

```csharp
public interface IIntegrationConnectionMonitor
{
    string PlatformName { get; }
    bool IsConnected { get; }
    string GetAuthorizationUrl();
    event EventHandler? ConnectionChanged;
    Task RefreshConnectionState(CancellationToken cancellationToken = default);
}
```

Define a platform-specific typed interface that extends it:

```csharp
// Thiccdal.Infrastructure.<Platform>
public interface I<Platform>ConnectionMonitor : IIntegrationConnectionMonitor { }
```

### 2. Implementation

Singleton in `Thiccdal.Remote.<Platform>`:
- Constructor-inject `I<Platform>TokenManager` (for `GetAuthorizationUrl`) and `IDbContextFactory<ApplicationDbContext>` (safe for singletons).
- `RefreshConnectionState` queries the DB for a valid non-expired token.
- Raises `ConnectionChanged` **only when the state actually changes** (avoids spurious renders).

### 3. DI registration (shared singleton pattern)

Register once as the concrete type, then forward to both interfaces:

```csharp
services.AddSingleton<TwitchConnectionMonitor>();
services.AddSingleton<ITwitchConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
```

This lets Blazor components inject `ITwitchConnectionMonitor` for type safety and the generic `IEnumerable<IIntegrationConnectionMonitor>` for platform-agnostic rendering.

### 4. OAuth callback wiring

In the minimal API callback, after persisting the token, call `RefreshConnectionState` so subscribed Blazor circuits re-render before redirect:

```csharp
app.MapGet("/auth/<platform>/callback", async (string code, I<Platform>TokenManager tm, I<Platform>ConnectionMonitor monitor, CancellationToken ct) =>
{
    await tm.StoreToken(code, ct);
    await monitor.RefreshConnectionState(ct);
    return Results.Redirect("/admin");
});
```

### 5. Blazor component subscription

```razor
@implements IDisposable

protected override void OnInitialized()
{
    Monitor.ConnectionChanged += OnConnectionChanged;
}

private void OnConnectionChanged(object? sender, EventArgs e)
    => InvokeAsync(StateHasChanged);

public void Dispose()
    => Monitor.ConnectionChanged -= OnConnectionChanged;
```

## Examples

- `src\Remote\Thiccdal.Remote.Twitch\TwitchConnectionMonitor.cs`
- `src\Thiccdal.Infrastructure\Integrations\IIntegrationConnectionMonitor.cs`
- `src\Modules\Thiccdal.Modules.ChatBot\ChatBotRegistrationExtension.cs`

## Anti-Patterns

- Don't register the monitor as `Scoped` — Blazor circuits share one instance and subscribe to events; scope boundaries break this.
- Don't call `RefreshConnectionState` from inside `ConnectionChanged` handlers — causes re-entrant loops.
- Don't expose mutable state directly on the monitor beyond `IsConnected`; keep additional detail in the token manager or DB.
