---
name: "merge-gated-issue-status-pass"
description: "Prepare or execute GitHub issue status updates by comparing issue acceptance text to landed repo state, closing only exact matches."
domain: "github-hygiene"
confidence: "high"
source: "earned"
tools:
  - name: "gh"
    description: "Read issue bodies, labels, and current state before commenting or closing."
    when: "When GitHub issues are the delivery contract for a feature batch."
---

## Context
Use this when a work batch landed partially, or when implementation drift means some issues are only partly satisfied even though the repo clearly moved forward.

## Pattern
- Read the issue body, not just the title; stale acceptance text is the main source of bad closes.
- Compare the issue text to landed code and verified tests, not to plans or in-progress working-tree intent.
- Close only exact matches.
- For partial matches, leave the issue open and post a concise progress note that names what shipped and what is still missing.
- If a newer issue supersedes an older one, call that out explicitly so the backlog stays legible.

## Thiccdal example
- During the 2026-05-29 Twitch Helix audit, `#166` was a close candidate once code lands, while `#167`, `#169`, and `#171` needed progress comments because the repo had only part of each original ask.

## Anti-patterns
- Do not close issues from local-only or obviously unlanded work.
- Do not treat Twitch-specific UI progress as completion for generic multi-platform operator UI issues.
- Do not skip test/build evidence when the user has asked for completion to mean verified code.
