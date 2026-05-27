# Blazor Route Smoke Tests

## When to use
Use this when a Blazor route changes the URL but the expected page does not render, or when a page can be hit through both direct navigation and in-app routing.

## Pattern
1. Put all routable module assemblies in one shared catalog.
2. Feed that catalog to both `<Router AdditionalAssemblies=...>` and `app.MapRazorComponents<App>().AddAdditionalAssemblies(...)`.
3. For page-level cancellation, create a private `CancellationTokenSource` field in the component instead of injecting one from DI.
4. Add `WebApplicationFactory<Program>` smoke tests that GET the affected route and one neighboring route to verify both render successfully.

## Thiccdal example
- Route catalog: `src\Thiccdal\RouteAssemblyCatalog.cs`
- Router: `src\Thiccdal\Components\Routes.razor`
- Endpoint mapping: `src\Thiccdal\Program.cs`
- Smoke tests: `src\Tests\Thiccdal.Tests\RouteRenderingTests.cs`
