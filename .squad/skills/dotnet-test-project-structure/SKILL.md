---
name: "dotnet-test-project-structure"
description: "Correct placeholder .NET test project names and folder nesting"
domain: "dotnet"
confidence: "high"
source: "earned (Twitch integration test project structure correction)"
---

## Context

New .NET test projects can accidentally keep template names like `TestProject1` or end up nested one folder too deep. In this repo, the project file should sit at the folder that matches the solution path and project name.

## Pattern

- Move the `.csproj` and source files to the folder that matches the intended project name
- Rename the `.csproj` to the final project name so the default assembly name follows suit
- Recalculate every relative `ProjectReference` after the move; the `..\` depth usually changes
- Update the `.slnx` entry to the new project path
- Replace leftover placeholder namespaces so test discovery and output use the correct project identity
- Validate with solution restore/build and targeted `dotnet test` for the renamed project

## Example

From:

`src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TestProject1\TestProject1.csproj`

To:

`src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj`

After moving, update references like:

- `..\..\..\..\Thiccdal.Data\...` → `..\..\..\Thiccdal.Data\...`
- `..\..\..\..\Remote\Thiccdal.Remote.Twitch\...` → `..\..\..\Remote\Thiccdal.Remote.Twitch\...`

## Anti-Patterns

- Leaving template project names in committed test assemblies
- Moving files without fixing relative `ProjectReference` paths
- Updating the project file but forgetting the `.slnx` path
- Assuming passing compile output proves the solution reference is correct
