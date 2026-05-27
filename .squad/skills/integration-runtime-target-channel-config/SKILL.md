# SKILL: Runtime Integration Target Configuration

## When to Apply

Use when an authenticated integration account (bot/service user) must act in a target owned by someone else, and operators need to change that target from the UI without editing config files.

## Pattern

1. Keep identities explicit in contracts:
   - bot/service identity
   - target channel/resource identity
   - target owner/broadcaster ID when platform APIs need a numeric identifier
2. Persist UI-selected target overrides in the application database, not `appsettings.json`.
3. Resolve a runtime profile object that combines:
   - immutable/default bot identity from options
   - mutable target override from storage
4. Let the live integration service subscribe to profile changes so active connections can switch targets safely.

## Thiccdal Example

- `TwitchOptions.BotUsername`
- `TwitchOptions.DefaultTargetChannel`
- `TwitchOptions.DefaultBroadcasterId`
- `TwitchChatConnectionProfile`
- `TwitchTargetChannelService`
- `TwitchSettingsService` delegating UI calls to the integration service

## Why

- Prevents confusing the authenticated bot account with the broadcaster/channel owner.
- Avoids brittle runtime writes to configuration files.
- Creates a clean seam for future API calls that need both login identity and owner identity.

## Tests to Include

1. Defaults resolve correctly when no override exists.
2. Updating the target persists normalized values.
3. Bot identity remains unchanged when the target changes.
4. Invalid target names are rejected.
5. Active API calls use the updated owner/broadcaster ID.
