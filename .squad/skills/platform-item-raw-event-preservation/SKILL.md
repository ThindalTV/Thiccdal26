---
name: "platform-item-raw-event-preservation"
description: "Preserve per-item raw payloads and source event names when normalizing polled platform events."
domain: "integrations"
confidence: "high"
source: "earned"
---

## Context
Use this when a polling adapter returns batches of heterogeneous vendor items and only some item types have first-class typed mappings.

## Patterns
- Serialize and persist each vendor item independently instead of storing the entire poll envelope on every normalized event.
- Copy the vendor event name into a dedicated field like `SourceEventType` even when the normalized `PlatformEventType` collapses multiple vendor types.
- Route unknown item types to a raw/base event shape at debug-level logging so diagnostics stay available without polluting normal operator surfaces.
- Keep typed chat/event mapping separate so non-chat items never flow down the chat path accidentally.

## Examples
- `src\Remote\Thiccdal.Remote.YouTube\YouTubeLiveChatMessageMapper.cs`
- `src\Thiccdal.Data\PlatformEventRecordFactory.cs`
- `src\Thiccdal.Data\Migrations\20260527214300_AddPlatformSourceEventType.cs`

## Anti-Patterns
- Reusing whole-poll JSON as `RawData` for every item.
- Mapping unknown vendor items to `ChatMessage` just because they arrived from a chat endpoint.
- Dropping the original vendor type name once it has been normalized to a coarse enum.
