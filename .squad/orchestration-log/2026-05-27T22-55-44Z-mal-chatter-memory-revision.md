# Orchestration Log: Mal (Lead Orchestrator / Architecture)

**Timestamp:** 2026-05-27T22:55:44Z  
**Batch:** Chatter Memory Revision & Non-Destructive Reset  
**Status:** Complete  
**Related Inbox:** `mal-chatter-memory-revision.md`

## Summary

Mal revised the chatter-memory implementation to resolve Jayne's operator-facing control blockers, replacing destructive clear/clear-all semantics with non-destructive reset markers while preserving the existing scoped-memory architecture and data strategy.

## Work Completed

### Architecture Reconciliation
- Identified Jayne's two core blockers: missing operator-facing reset UI and destructive clear semantics
- Designed non-destructive reset via `ChatterMemoryReset` marker table (stores reset timestamps)
- Preserved existing memory derivation from `PlatformUsers` + `ChatMessages` (no new persistent storage)
- Confirmed reset semantics: memory reads honor latest applicable reset cutoff while source records remain intact

### Interface & Service Updates
- Updated `IChatterMemoryService` from `Clear/ClearAll` (destructive) to `Reset/ResetAll` (non-destructive)
- Supports two reset scopes:
  - **Scoped:** One exact `{platform, channel, platformUserId}` tuple
  - **Global:** All scopes across all platforms/channels
- Service now filters memory derivation by reset marker timestamp (ignores older chat messages)

### Operator Surface
- Added `/chatbot` route with reset controls (scoped + global buttons)
- New Blazor component wires UI buttons to `IChatterMemoryService.Reset(...)` and `ResetAll(...)`
- Operator gains immediate visibility and control over memory context without source data loss

### Scope & Guardrails Preserved
- Memory remains strictly keyed to `{platform, channel, platformUserId}` tuple
- Reset operation is auditable (timestamp recorded in `ChatterMemoryReset`)
- No cross-platform identity merging
- Only sanitized public-chat facts injected (sensitive data filtering unchanged)
- AI replies still routed only to originating platform/channel

## Design Justification

Non-destructive reset is the minimal safe adjustment:
- Existing architecture derives memory directly from persisted chat history (no summary table)
- Reset-marker design keeps that architecture intact
- Avoids data loss that destructive delete would cause
- Operators gain immediate memory suppression without audit trail loss

## Handoff Status

Revision complete and ready for Jayne's security re-review. All blocking architectural concerns resolved. No new schema table beyond reset marker (minimal footprint).

## Related Documents

- `mal-chatter-memory-revision.md` (inbox, to merge to decisions.md)
- `jayne-chatter-memory-rereview.md` (inbox, pending merge to decisions.md)
