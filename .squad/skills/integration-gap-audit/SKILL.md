---
name: "integration-gap-audit"
description: "Audit an external-platform adapter against architecture, backlog intent, and vendor API capabilities."
domain: "integrations"
confidence: "high"
source: "earned"
tools:
  - name: "gh"
    description: "Inspect issue titles, bodies, and labels to compare implementation with planned backlog slices."
    when: "When the repo uses GitHub issues as the delivery contract."
---

## Context
Use this when an integration has drifted from the architecture or when a platform API changed and you need a no-code audit before reimplementation.

## Patterns
- Read the current adapter code, the shared infrastructure contracts, and the user-facing consumer path together; gaps often show up at the boundary, not just in the adapter.
- Compare the code to both the architecture doc and the GitHub issue bodies; issues often reveal intended seams that are missing in code.
- Cross-check the target platform's official docs for transport, auth scopes, and payload shape before deciding whether to preserve or replace an implementation.
- Report outcomes by category: transport/chat, emotes/media, event ingestion, auth, normalization, and downstream presentation.
- End with a clear split of what to replace versus what to preserve so follow-on issues can be labeled cleanly.

## Examples
- Current Twitch audit in Thiccdal compared `src\Remote\Thiccdal.Remote.Twitch\`, `src\Thiccdal.Infrastructure\`, `docs\architecture\overview.md`, and Phase 5 GitHub issues to show that the code is IRC-only while the target requires Helix + EventSub and richer normalization into the prompter path.

## Anti-Patterns
- Don't judge the adapter by vendor SDK presence alone; inspect the actual data that reaches consumers.
- Don't mix core adapter work with teleprompter or overlay rendering scope; route those with separate area labels after the normalization seam is clear.
- Don't rely on unofficial summaries for platform capabilities when official docs are available.
