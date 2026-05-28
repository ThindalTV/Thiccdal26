# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Backend services, bot handlers, data flow | Kaylee | Chatbot services, EF Core wiring, command handling, persistence |
| Blazor UI, UX, operator workflows | Inara | Control screens, touch-friendly UI, components, interaction design |
| User-facing docs and help content | Book | `docs\help\`, onboarding, operator instructions, usage guides |
| Platform adapters and external integrations | River | Twitch integration, cross-platform chat/event connections, external APIs |
| Security review and penetration testing | Jayne | Threat review, auth hardening, secret handling, security tests |
| GitHub workflow, issue hygiene, delivery status | Zoe | Issue triage support, PR status, work-item tracking, branch/PR coordination |
| Architecture, scope, review gates | Mal | Cross-cutting design, trade-offs, reviewer decisions, routing for team work |
| Code review | Mal | Review PRs, check quality, suggest improvements |
| Testing | Mal | Coordinate test ownership when no dedicated tester is present |
| Scope & priorities | Mal | What to build next, trade-offs, decisions |
| Session logging | Scribe | Automatic — never needs routing |
| Queue monitoring and next-work pickup | Ralph | Watch backlog, detect stalled work, keep the board moving |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
8. **Zoe vs. Ralph split** — Zoe handles human-facing GitHub coordination and status. Ralph handles continuous monitoring and next-work detection.
