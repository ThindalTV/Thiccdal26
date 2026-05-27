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
📌 Issue routing completed on 2026-05-28: All 152 open issues labeled with squad and squad:{member} tags

## Learnings

- Zoe owns issue and PR hygiene, but Ralph owns continuous board monitoring.
- Zoe should make status legible for the team and the user.
- **Project structure:** .NET Aspire-based system; Blazor Server host controls platform adapters (Twitch, YouTube, Discord, Facebook, X). Streaming (RTMP relay), overlay SignalR, teleprompter UI, and operator control UI are the main modules.
- **Issue phases:** 11 (overlay), 12 (status API), 13 (identity merge), 14 (hardening), 15 (operator state), 16 (pre-live checklist) — currently at phase 16.
- **PR board:** Zero open PRs (clean state, ready for new work).
- **Backlog status:** 152 open issues across phases, all now routed to squad members:
  - **squad:inara** (48 issues): UI/UX work — operator UI, overlay, teleprompter
  - **squad:kaylee** (28 issues): Backend work — EF Core, data, chat services, APIs, streaming
  - **squad:mal** (48 issues): Testing and architecture — all test issues plus architectural decisions
  - **squad:river** (28 issues): Platform integrations — Twitch, YouTube, Discord, Facebook, X, TikTok, LinkedIn
- **Routing logic:** Issues routed by primary area of concern using `area/*`, `type/*`, and `phase/*` labels as signals. Type/test issues go to Mal (testing coordination). UI-related work goes to Inara. Backend services go to Kaylee. Platform-specific integration work goes to River. Infrastructure/architecture work goes to Mal.
- **Helix issue hygiene:** For Twitch Phase 17+ work, close only issues whose original acceptance text is fully satisfied by landed code. Use progress comments instead of closure when the repo has partial scope shifts (`#167`, `#169`, `#171`, `#96` were all partials during the 2026-05-29 audit).
- **Helix audit sources:** Post-batch issue status checks should read `docs\architecture\helix-redesign.md`, `.squad\log\2026-05-29T00-00-00Z-helix-foundation.md`, `.squad\log\2026-05-29T01-10-00-twitch-auth-batch.md`, and the matching River/Kaylee/Inara orchestration logs before touching GitHub.
- **Validation baseline:** `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj --no-restore` passed with 41 tests and `dotnet test src\Tests\Thiccdal.Tests\Thiccdal.Tests.csproj --no-restore` passed with 3 route tests during the 2026-05-29 audit. Full `dotnet build Thiccdal.slnx -warnaserror` was blocked by a running `Thiccdal.Aspire.AppHost.exe` file lock, so treat that specific failure as environment contention rather than immediate code drift.
