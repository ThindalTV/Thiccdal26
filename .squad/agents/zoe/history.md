# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Zoe handles GitHub coordination, work-item clarity, and delivery status.

## Recent Updates

📌 Firefly squad configured on 2026-05-27  
📌 Initial GitHub backlog scan completed on 2026-05-26

## Learnings

- Zoe owns issue and PR hygiene, but Ralph owns continuous board monitoring.
- Zoe should make status legible for the team and the user.
- **Project structure:** .NET Aspire-based system; Blazor Server host controls platform adapters (Twitch, YouTube, Discord, Facebook, X). Streaming (RTMP relay), overlay SignalR, teleprompter UI, and operator control UI are the main modules.
- **Issue phases:** 11 (overlay), 12 (status API), 13 (identity merge), 14 (hardening), 15 (operator state), 16 (pre-live checklist) — currently at phase 16.
- **PR board:** Zero open PRs (clean state, ready for new work).
- **Backlog status:** 50+ open issues, phase-organized, no assignees yet — Mal to route via squad-member labels; Ralph monitors for stalls.
