# SKILL: RTMP relay destination provider

## When to Apply

Use when a platform adapter can participate in Thiccdal's restream fanout only if it can expose a concrete outbound RTMP publish URL.

## Pattern

1. Keep the broad platform seam as `IStreamTarget` / `IPlatformConnection`.
2. Add an **optional** `IRtmpRelayDestinationProvider` implementation on adapters that can actually resolve a publish destination.
3. Return `null` when the adapter exists but the RTMP destination is not configured yet.
4. Register the concrete adapter as `IRtmpRelayDestinationProvider` in the adapter's registration extension.
5. Let `RestreamRuntimeService` and `RtmpFanoutService` treat relay support as explicit capability, not inferred magic.

## Why

This keeps the integration boundary honest. A platform can be connected for chat/events without pretending it is relay-ready, and the operator surface can explain the exact gap instead of reporting fake fanout success.

## Examples

- `src\Remote\Thiccdal.Remote.Null\NullPlatformConnection.cs`
- `src\Remote\Thiccdal.Remote.LinkedIn\LinkedInService.cs`
- `src\Remote\Thiccdal.Remote.TikTok\TikTokService.cs`
- `src\Thiccdal.Data\RestreamRuntimeService.cs`
- `src\Thiccdal.Streaming\RtmpFanoutService.cs`
