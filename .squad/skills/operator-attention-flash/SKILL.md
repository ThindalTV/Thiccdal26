---
name: "operator-attention-flash"
description: "Drive transient operator attention flashes from shared state snapshots without duplicating event pipelines."
domain: "ui-state"
confidence: "high"
source: "observed"
---

## Context
Use this when a Blazor operator surface needs a brief attention cue for new items in shared state, especially when one surface already owns the detailed feed and another only needs a visual nudge.

## Patterns
1. Keep one source of truth for queue state (`QuestionOverlayService`) and consume snapshots rather than inventing a second flash-specific bus.
2. On cross-surface pages, prefer the broader shared seam (`IOperatorStateService`) if it already forwards the underlying question state.
3. Cache the last relevant count locally in the component and trigger the flash only when the count increases.
4. Use a local version counter plus delayed reset so overlapping flashes do not prematurely clear a newer flash.
5. Keep distinct flash meanings visually separate (for example, question vs. significant-event) instead of collapsing them into one generic alert.

## Examples
- Queue source: `src\Thiccdal.Infrastructure\Questions\QuestionOverlayService.cs`
- Shared operator seam: `src\Thiccdal.Infrastructure\Operators\OperatorStateService.cs`
- Existing count-delta flash pattern: `src\Modules\Thiccdal.Modules.Teleprompter\Pages\Prompter.razor`

## Anti-Patterns
- Adding a duplicate chat feed to the dashboard when the prompter already owns chat visibility.
- Triggering the flash for every state mutation, including selection or dismissal changes that do not represent a newly queued question.
- Introducing a new event path solely for UI flash state when the queue snapshot already exposes the needed delta.
