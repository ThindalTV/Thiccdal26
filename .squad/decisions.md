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
