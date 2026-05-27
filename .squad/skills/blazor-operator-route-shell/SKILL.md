# SKILL: Blazor Operator Route Shell

## What It Is

A pattern for routed operator pages that are launched from dashboard controls but still need to feel like part of the live control surface.

## Use When

- A top-bar chip or operator control navigates to a dedicated route.
- The destination is operational UI, not a generic settings/admin page.
- The current bug is "URL changed but the visible dashboard stayed put" or the route renders in the wrong shell.

## Pattern

1. Keep the route on a dedicated page component.
2. Render that page with `DashboardLayout` and include the control `TopBar`.
3. Add a clear, touch-friendly path back to the dashboard.
4. If the page needs a cancellation token for async work, create a private `CancellationTokenSource` in the component and dispose it in `DisposeAsync`.
5. Avoid injecting `CancellationTokenSource` from DI for routed Blazor pages.

## Thiccdal Example

- `src\Thiccdal\Components\Pages\TwitchConnect.razor`
- `src\Thiccdal\Components\Pages\TwitchConnect.razor.css`
- `src\Modules\Thiccdal.Modules.Control\Components\TopBar\TopBar.razor`
- `src\Tests\Thiccdal.Tests\RouteRenderingTests.cs`

## Why It Works

- Operators keep the same visual shell and header context when moving from the dashboard into a focused integration workflow.
- The route destination controls its own async lifetime, so navigation does not depend on a missing DI registration.
