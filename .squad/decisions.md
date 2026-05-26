# Squad Decisions

## Active Decisions

### 2026-05-26: GitHub Backlog Baseline Established
**Agent:** Zoe (GitHub Sync / Status / Work Items)  
**What:** Initial GitHub issues scan complete. 50+ open issues across phases 11–16. Phase 16 (Pre-Live Checklist) is current focus with 17 issues. Zero open PRs. No assignees recorded yet.  
**Why:** Establishes backlog visibility and readiness state for squad triage. Required for phase-16 work assignment.

### 2026-05-26: Repository Structure Confirmed
**Agent:** Mal (Lead / Orchestrator)  
**What:** On-disk structure verified against `docs/architecture/overview.md`. All expected modules, platform adapters, test projects present and correctly placed. Configuration pattern (IOptions<T>) consistent throughout. No corrective actions needed.  
**Why:** Validates architecture documentation accuracy and confirms readiness for ongoing feature work. Structure alignment enables confident adoption of established conventions (file-scoped namespaces, interface-driven design, test-per-project pattern).

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
### 2026-05-27: Firefly squad roster adopted
**By:** ThindalTV (via Squad)
**What:** Use a Firefly-based persistent roster for Thiccdal: Mal, Kaylee, Inara, Book, River, Jayne, Zoe, plus Scribe and Ralph.
**Why:** The user explicitly wanted the team identity and responsibilities mapped to Firefly characters for long-term squad use.

### 2026-05-27: Zoe and Ralph responsibilities are distinct
**By:** ThindalTV (via Squad)
**What:** Zoe owns GitHub sync, status reporting, and work-item coordination. Ralph owns continuous backlog monitoring, stalled-work detection, and next-item pickup.
**Why:** This keeps project coordination separate from the persistent queue-monitor role while preserving both members on the roster.

### 2026-05-27: Inline Twitch Authentication Directive
**By:** ThindalTV (via Copilot)
**What:** Use inline Twitch authentication rather than config-based credentials. On first bot startup, open a Twitch login window for authentication, then remember the resulting auth for later runs.
**Why:** User request — captured for team memory; improves UX (no credentials in config file) and security (OAuth flow).

### 2026-05-28: Twitch Helix EventSub Architecture Decision
**By:** Mal (Lead / Orchestrator)
**Requested by:** ThindalTV
**Status:** Approved for implementation planning (Phases 17–20, 23 GitHub issues)
**What:** Replace raw TCP/IRC with pure EventSub WebSocket for Twitch chat and platform events. Introduce ChatFragment hierarchy (TextFragment, EmoteFragment, CheermoteFragment) for structured emote/badge rendering. Implement inline OAuth flow (operator login on first run, token persisted in SQLite). Define typed PlatformEvent subtypes (TwitchFollowEvent, TwitchSubscribeEvent, TwitchCheerEvent, TwitchRaidEvent, TwitchRedeemEvent).
**Why:** 
- Current IRC is insecure (raw TCP, no TLS, no tags), plain-text only (no emotes/badges/events)
- Twitch no longer recommends IRC; EventSub is official path
- Enables rich chat rendering, subscriber/cheerer awareness, event-driven overlays
- Deterministic emote CDN URLs (no HTTP lookup required)
**Key decisions:**
- Pure EventSub only (not IRC + EventSub hybrid for MVP)
- ChatEvent.Content stays as plain-text fallback for backward compatibility
- 6+ new OAuth scopes required; startup validates and prompts for re-auth if needed
- Four-phase rollout: Foundation (EventSub ingest), Rich Chat (fragments + emotes), Full Events (all typed events + event bus), Stream Info (Helix metadata)
**Preserved user directives:** Inline Twitch auth; open questions (cheer threshold, bot mod status, animated vs static default)
**Architecture document:** `docs/architecture/helix-redesign.md`

### 2026-05-28: GitHub Issue Routing and Squad Labeling Complete
**By:** Zoe (GitHub Sync / Status / Work Items)
**Status:** Complete
**What:** Routed all 152 open GitHub issues to appropriate squad members via `squad:` labels. Closed Phase 5 IRC issues #24–31 as superseded by Helix redesign. Created Phase 17–20 labels and staged 23 new Helix implementation issues.
**Routing summary:**
- Inara (Frontend/UX): 48 issues (operator UI, overlay, teleprompter)
- Kaylee (Backend): 28 issues (data, chat, APIs, streaming)
- Mal (Testing/Architecture): 48 issues (type/test, area/tests, area/infrastructure)
- River (Integrations): 28 issues (platform adapters)
**Why:** Enables squad visibility, parallelizes work, clarifies ownership per agent expertise
