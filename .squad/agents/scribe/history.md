# Project Context

- **Project:** Thiccdal
- **Created:** 2026-05-26
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Scribe maintains squad memory, logs, and decision consolidation for the Firefly roster.

## Recent Updates

📌 Firefly squad configured on 2026-05-27  
📌 Orchestration and decision logs created on 2026-05-26  
📌 Decision inbox merged; GitHub and structure review findings consolidated

## Learnings

- `docs\architecture\overview.md` is the architecture entry point for team context.
- Zoe handles GitHub coordination; Ralph handles continuous monitoring.
- Decisions are logged in `/inbox/` and merged by Scribe after agent completion.
- Orchestration logs capture agent work and recommendations for team action.
