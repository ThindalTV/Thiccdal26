# ADR: RTMP ingest and relay implementation

**Date:** 2026-05-31  
**Status:** Accepted

## Context

Phase 8 needs a real RTMP ingest listener, real fanout orchestration, and a BRB fallback path inside `Thiccdal.Streaming`. The repo already owns the operator control plane and typed platform adapters, but it does not have a native RTMP server or a safe way to push one ingest stream to multiple external RTMP destinations.

## Options considered

### 1. FFmpeg only

- **Pros:** excellent for copy/transcode, already the right tool for BRB media loops and recording.
- **Cons:** FFmpeg cannot honestly act as the RTMP ingest server for OBS, so it does not satisfy #75 by itself.

### 2. node-media-server or another sidecar

- **Pros:** proven RTMP ingest path, common restream pattern.
- **Cons:** adds a second runtime stack, another deployment artifact, and an ownership split between the .NET host and a Node process. It also weakens typed integration seams because the control plane would configure a sidecar rather than a first-class .NET service.

### 3. Custom RTMP server in Thiccdal

- **Pros:** full control.
- **Cons:** high protocol risk and too much surface area for the current phase. A home-grown RTMP implementation would burn time before fanout or BRB behavior became usable.

### 4. LiveStreamingServerNet + FFmpeg

- **Pros:** gives Thiccdal a native .NET RTMP ingest listener now, keeps lifecycle control in-process, and still uses FFmpeg where FFmpeg is strongest: per-destination relay processes and BRB slate generation.
- **Cons:** introduces one new library plus FFmpeg process management, and relay readiness still depends on platform adapters exposing concrete RTMP destinations.

## Decision

Use **LiveStreamingServerNet** for RTMP ingest and **FFmpeg** for outbound relay/BRB processes.

## Consequences

- `RtmpIngestListener` is implemented in-process and owns the OBS-facing RTMP server lifecycle.
- `RtmpFanoutService` starts isolated FFmpeg publish processes per active relay destination so one target failure does not take down the others.
- `BrbSlateInjector` reuses the same FFmpeg process seam for slate injection.
- Adapter participation is explicit: a platform only joins fanout when it exposes `IRtmpRelayDestinationProvider`.

## Current honest scope

- Implemented now: single-publisher ingest, state transitions (`WaitingForIngest` / `Live` / `BrbSlate` / `Error`), isolated relay-process orchestration, and BRB process orchestration.
- Still future work: richer media health telemetry, bundled default BRB asset, and broader platform RTMP destination coverage for adapters that do not yet expose publish URLs.
