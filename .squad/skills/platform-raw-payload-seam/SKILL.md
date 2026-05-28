---
name: "platform-raw-payload-seam"
description: "Keep normalized chat/event persistence working by storing raw payloads at the item level expected by downstream resolvers."
domain: "integrations"
confidence: "high"
source: "earned"
---

## Context

Use this when a platform adapter polls or batches multiple messages/events in one API response, but downstream persistence or normalization logic still needs to recover per-message identity from `RawData`.

## Patterns

- If downstream code resolves platform user ids from `RawData`, serialize the **individual item/event payload**, not the whole batch response.
- Check the consumer seam before finalizing mapper shape. In Thiccdal, `PlatformUserIdResolver` expects fields like `authorDetails.channelId` at the raw payload root.
- Treat raw-payload granularity as part of the contract, not as incidental debugging data.
- Verify both the catch-all event mapper and the chat mapper use the same item-level payload rule so unknown-event storage and chat persistence stay consistent.
- Add a focused test that proves the persisted raw payload shape is sufficient for downstream user-id resolution.
- When the platform exposes a vendor-specific event kind that should survive normalization, carry it separately (for example `PlatformEvent.SourceEventType`) instead of abusing the shared enum.

## Examples

- `src\Remote\Thiccdal.Remote.YouTube\YouTubeLiveChatMessageMapper.cs`
- `src\Thiccdal.Data\PlatformUserIdResolver.cs`
- `src\Thiccdal.Infrastructure\Bot\Models\PlatformEvent.cs`
- Phase 6 YouTube review: whole-poll JSON caused YouTube chat persistence to fall back from `authorDetails.channelId` to display name.

## Anti-Patterns

- Serializing the full poll envelope into every mapped event.
- Assuming `RawData` is only for diagnostics; in this repo it can be part of the normalization seam.
- Fixing user-id resolution in one mapper path but leaving the unknown-event catch-all on a different raw payload shape.
- Stuffing vendor-specific event names into the shared normalized enum when a dedicated source-type field would keep consumers cleaner.
