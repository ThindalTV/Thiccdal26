# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Mal leads cross-cutting decisions and reviewer gates for the Firefly squad.

## Recent Updates

📌 Firefly squad configured on 2026-05-27
📌 Orchestration and decision logs created on 2026-05-26

## Learnings

- `docs\architecture\overview.md` is the first-stop architecture document.
- Zoe owns GitHub coordination; Ralph owns continuous monitoring.
- Phase-based issue workflow is effective for visibility and sequencing.
- Test-per-project structure is established and ready for extension.

### 2026-05-26: Repository Structure Review — Repository-Architecture Alignment

**Finding:** The on-disk directory structure is **well-aligned** with the documented architecture. The solution layout mirrors the intended module and platform separation cleanly.

**Structure confirmed:**
- `/src/Thiccdal/` — Blazor Server host (host app, no business logic beyond routing)
- `/src/Thiccdal.Data/` — EF Core DbContext, entities, migrations
- `/src/Thiccdal.Infrastructure/` — Interfaces (IPlatformConnection, IChatService, etc.), enums, value types
- `/src/Thiccdal.API/` — Status endpoint and related controllers (phase 12 work: /status, /status/badge.svg)
- `/src/Thiccdal.Streaming/` — RTMP ingest, fanout, recording
- `/src/Modules/` — Four RCL projects: Control (operator UI), Overlay, Teleprompter, ChatBot
- `/src/Shared/` — Thiccdal.Shared.Components (input primitives, shared models)
- `/src/Remote/` — Eight platform adapters (Twitch, YouTube, Facebook, X, Discord, LinkedIn, TikTok, Null)
- `/src/Aspire/` — AppHost and ServiceDefaults
- `/src/Tests/` — Thiccdal.Data.Tests, Thiccdal.Tests, Remote/Thiccdal.Remote.Twitch.Tests

**Notes:**
- All expected platform adapters present; LinkedIn and TikTok correctly marked as disabled-until-approved.
- Test structure follows convention: test project per source project.
- Solution file (Thiccdal.slnx) reflects the disk layout accurately.
- GitHub Issues: 161 total, well-organized by phase (currently phase 16: Pre-Live Checklist). Phases progress from 11 (Overlay) through 16 (Pre-Live Checklist), with clear feature/test/fix categorization.

**Recommendations for future work:**
- Maintain the phase-based issue workflow; it provides clear visibility and sequencing.
- When adding new modules or platforms, replicate the test-per-project structure.
- Configuration follows IOptions<T> pattern consistently; no IConfiguration magic strings observed.
- Glassmorphic CSS conventions are defined in architecture doc (§6) — future CSS work should reference this.
