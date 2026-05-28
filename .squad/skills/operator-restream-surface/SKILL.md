---
name: "operator-restream-surface"
description: "Add a truthful operator-facing restream control surface that spans pre-live configuration and live runtime access."
domain: "blazor-ui"
confidence: "high"
source: "earned"
tools:
  - name: "dotnet build"
    description: "Verifies UI/API/runtime seams compile together."
    when: "After changing restream UI, API contracts, or runtime registration."
  - name: "dotnet test"
    description: "Verifies route rendering, operator settings, and restream API behavior."
    when: "After touching the restream operator surface or its backend seam."
---

## Context
Use this when a feature needs to expose restream or other live-runtime controls honestly before the full media backend is complete.

## Patterns
1. Reuse the existing operator component shell before inventing a new surface.
2. Put configuration-time access in `OperatorSettingsDialog`.
3. Put live-runtime access on the live dashboard toolbar.
4. Back both surfaces with one narrow API seam and one reusable panel.
5. Keep destination rows presentational and parameter-driven.
6. Show explicit dependency notes when runtime implementation is partial instead of overstating capability.

## Examples
- `src\Modules\Thiccdal.Modules.Control\Components\Restream\RestreamPanel.razor`
- `src\Modules\Thiccdal.Modules.Control\Components\Restream\RestreamDestination.razor`
- `src\Modules\Thiccdal.Modules.Control\Components\Settings\OperatorSettingsDialog.razor`
- `src\Modules\Thiccdal.Modules.Control\Pages\Dashboard.razor`
- `src\Thiccdal.API\Restream\RestreamApiExtensions.cs`
- `src\Thiccdal.Data\RestreamRuntimeService.cs`
- `src\Tests\Thiccdal.Tests\OperatorSettingsDialogTests.cs`
- `src\Tests\Thiccdal.Tests\RestreamApiTests.cs`

## Anti-Patterns
- Do not create separate pre-live and live restream components with duplicated logic.
- Do not claim the relay pipeline is fully implemented if the backend currently exposes only persisted config and runtime seams.
- Do not bury restream only in settings or only in the live toolbar; operators need both entry points.
