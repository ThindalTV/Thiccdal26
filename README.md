# Thiccdal

[![CI](https://github.com/ThindalTV/Thiccdal26/actions/workflows/ci.yml/badge.svg)](https://github.com/ThindalTV/Thiccdal26/actions/workflows/ci.yml)
[![Coverage](https://codecov.io/gh/ThindalTV/Thiccdal26/branch/main/graph/badge.svg)](https://codecov.io/gh/ThindalTV/Thiccdal26)

Thiccdal is a **streaming command and control system** built with .NET 10 and Blazor Server. It runs on the stream PC and is operated remotely from a second device — typically a Surface Pro tablet — giving the streamer hands-free control over every aspect of a live broadcast.

## Features

- **Multicast RTMP** — single ingest fanned out to Twitch, YouTube, Facebook, X, and more simultaneously
- **Unified chat** — aggregated real-time feed from all connected platforms
- **Chatbot** — command-based bot driven by the unified chat pipeline
- **Live overlay** — browser-source overlay with real-time control
- **Teleprompter** — scrollable on-camera script
- **Event tracking** — follows, subs, redeems, gifted subs, and other platform events
- **Stream recording** — local disk recording alongside the live broadcast

## Tech Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor Server |
| Database | SQLite via Entity Framework Core |
| Orchestration | .NET Aspire |
| Testing | xUnit |

## Getting Started

```bash
dotnet restore
dotnet build
dotnet run --project src/Aspire/AppHost
```
