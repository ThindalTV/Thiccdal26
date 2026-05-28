---
name: "stream-recording-process-persistence"
description: "Persist external recording-process lifecycle truthfully by creating the row at start intent and completing it on clean or failed shutdown."
domain: "streaming"
confidence: "high"
source: "earned"
---

## Context
Use this when a streaming or media feature launches an external recorder like FFmpeg and operators need trustworthy file-path, error, and start/stop history in SQLite.

## Patterns
1. Define the persistence seam in `Thiccdal.Infrastructure`, not in the streaming implementation.
2. Let `Thiccdal.Data` own the `StreamRecording` entity plus `IStreamRecordingService` implementation.
3. Create the DB row before or as the process is launched so the intended file path survives startup failures.
4. Complete the same row on every exit path, including explicit stop and unexpected process termination.
5. Expose the latest persisted snapshot through the runtime API instead of leaking EF entities or process handles.
6. Keep operator state honest: a listener waiting for ingest is not yet an active recording.

## Examples
- `src\Thiccdal.Infrastructure\Streaming\IStreamRecordingService.cs`
- `src\Thiccdal.Data\StreamRecordingService.cs`
- `src\Thiccdal.Data\Models\StreamRecording.cs`
- `src\Thiccdal.Streaming\DiskRecorder.cs`
- `src\Thiccdal.Streaming\FfmpegRecordingProcessRunner.cs`
- `src\Thiccdal.Data\RestreamRuntimeService.cs`

## Anti-Patterns
- Do not mark recording active just because the operator clicked Start if ingest has not gone live yet.
- Do not keep recording metadata only in memory or logs.
- Do not let `Thiccdal.Streaming` reach into `ApplicationDbContext` directly.
- Do not drop the file path when FFmpeg fails to start or exits unexpectedly.
