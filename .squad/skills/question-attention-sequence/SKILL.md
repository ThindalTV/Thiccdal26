---
name: "question-attention-sequence"
description: "Drive shared operator attention flashes from a monotonic question-state signal instead of per-surface event wiring."
domain: "blazor-ui"
confidence: "high"
source: "earned"
---

## Context
Use this when multiple Blazor operator surfaces need to react to the same "new item arrived" moment, but the project already has a shared state snapshot service and you want to avoid adding UI-only event channels.

## Patterns
1. Add a monotonic attention counter to the shared state snapshot (QuestionDashboardState.AttentionSequence).
2. Increment it only when a genuinely new item is queued (QuestionOverlayService.TryEnqueueDetectedQuestion, AddManualQuestion).
3. Do not increment it for follow-up mutations like select, promote, dismiss, or clear.
4. In each UI surface, cache the last seen sequence and flash only when the new snapshot has a larger value.
5. On first render, replay the flash if the current sequence is already greater than zero so reopened operator surfaces still get the alert.
6. Keep the visual treatment surface-specific: a queue header flash on the dashboard, a lighter attention pill on the prompter.

## Examples
- State seam: src\Thiccdal.Infrastructure\Questions\QuestionDashboardState.cs
- Counter ownership: src\Thiccdal.Infrastructure\Questions\QuestionOverlayService.cs
- Dashboard consumer: src\Modules\Thiccdal.Modules.Control\Components\Questions\QuestionQueuePanel.razor
- Prompter consumer: src\Modules\Thiccdal.Modules.Teleprompter\Pages\Prompter.razor
- Smoke coverage: src\Tests\Thiccdal.Tests\RouteRenderingTests.cs

## Anti-Patterns
- Do not add a separate dashboard-only feed or flash event when the queue state already knows a new question arrived.
- Do not trigger the attention counter on every queue mutation, or the operator will get noisy false positives.
- Do not make the prompter flash full-screen if the goal is operator awareness rather than interruption.
