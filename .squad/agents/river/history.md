# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

River handles platform adapters, integration seams, and external API contracts.

## Recent Updates

📌 Firefly squad configured on 2026-05-27

## Learnings

- Platform adapters implement shared infrastructure contracts and feed typed events into the system.
- Twitch work belongs with River unless it is purely UI or backend service wiring.
