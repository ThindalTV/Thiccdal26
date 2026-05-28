---
name: "development-live-dashboard-preview"
description: "Add a development-only shortcut that opens the live operator dashboard without invoking the real go-live pipeline."
domain: "blazor-ui"
confidence: "high"
source: "earned"
tools:
  - name: "dotnet test"
    description: "Verifies the UI control, route rendering, and operator-state transition."
    when: "After wiring a development-only dashboard preview or touching operator mode transitions."
---

## Context
Use this when operators need to inspect or develop the live dashboard UI while the system is technically offline. The goal is a truthful preview path, not a fake streaming start.

## Patterns
1. Put the shortcut on the pre-live operator surface, usually `TopBar`, so the action is available where operators already stage live actions.
2. Gate the control to Development only.
3. Reuse `IOperatorStateService` mode transitions instead of adding a one-off frontend flag.
4. Prefer `BeginLiveSession()` for entering the live shell and keep `SetActiveStreamState(null)` as the exit path through the existing Go Offline flow.
5. Add one component test for visibility/click behavior, one route smoke assertion for the rendered page, and one operator-state test proving the offline return path.

## Examples
- `src\Modules\Thiccdal.Modules.Control\Components\TopBar\TopBar.razor`
- `src\Modules\Thiccdal.Modules.Control\Components\TopBar\TopBar.razor.css`
- `src\Tests\Thiccdal.Tests\TopBarTests.cs`
- `src\Tests\Thiccdal.Tests\RouteRenderingTests.cs`
- `src\Tests\Thiccdal.Tests\OperatorStateServiceTests.cs`

## Anti-Patterns
- Do not expose the shortcut outside Development.
- Do not call the real go-live action service just to reach the live dashboard UI.
- Do not introduce a separate preview-only UI mode if the existing operator-state seam already covers the transition.
