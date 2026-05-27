---
name: shared-activity-feed
description: Centralize Blazor-facing chat and platform activity rendering behind one singleton feed.
---

# Shared Activity Feed

## Use when

- Multiple UI surfaces need the same live stream activity (chat + follows/raids/cheers/etc.).
- The project is not ready for a full event bus yet.
- You need recent history plus live updates for Blazor pages or components.

## Pattern

1. Define a small interface in Infrastructure (`IActivityFeedService`) that exposes cached entries plus an `EntryAdded` event.
2. Create a singleton implementation that subscribes to the normalized upstream contract (`IChatService` / `IPlatformEventSource`).
3. Register that same singleton as `IHostedService` so subscriptions are active before the upstream connection starts.
4. Put all formatting in one helper (`PlatformActivityFormatter`) so pages consume a stable `ActivityFeedEntry` shape.
5. Let pages own their own `CancellationTokenSource` and only handle UI concerns (binding, trimming local view state, `InvokeAsync(StateHasChanged)`).
6. When adding a new platform adapter, normalize raw vendor payloads before they hit the feed; the feed should never parse platform-specific JSON.
7. Persist upstream platform events before raising the feed entry so overlay/prompter diagnostics can always reconcile what was shown on screen.

## Thiccdal example

- Interface: `src\Thiccdal.Infrastructure\Bot\IActivityFeedService.cs`
- Feed model: `src\Thiccdal.Infrastructure\Bot\Models\ActivityFeedEntry.cs`
- Formatter: `src\Thiccdal.Infrastructure\Bot\PlatformActivityFormatter.cs`
- Implementation: `src\Modules\Thiccdal.Modules.ChatBot\Services\ActivityFeedService.cs`
- DI wiring: `src\Modules\Thiccdal.Modules.ChatBot\ChatBotRegistrationExtension.cs`
- Twitch upstream: `src\Remote\Thiccdal.Remote.Twitch\TwitchService.cs`

## Why it works

- Keeps platform event formatting in one place.
- Avoids duplicated Blazor page subscriptions and branching.
- Preserves a clean upgrade path to a future `IEventBus`.
- Lets adapter work ship independently from overlay/prompter presentation polish.
