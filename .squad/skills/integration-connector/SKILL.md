# SKILL: Reusable Integration Connector Component Pattern

## What It Is

A pattern for building reusable admin-side integration connection controls in Blazor Server. Provides a touch-friendly chip/pill in the TopBar that shows real-time connection state and stays actionable for the real integration-management flow.

## Files (Thiccdal)

```
src/Modules/Thiccdal.Modules.Control/Components/Integrations/
  IntegrationConnectionState.cs       — enum: Unknown | NotConnected | Connecting | Connected | Error
  IntegrationConnector.razor          — reusable chip/pill component (presentational)
  IntegrationConnector.razor.css      — scoped CSS, uses --plat-color CSS variable
  IntegrationAuthDialog.razor         — modal overlay for auth flow (presentational)
  IntegrationAuthDialog.razor.css     — scoped CSS, matches glass-bg/glass-border theme
```

## Component Contract

### IntegrationConnector
```razor
<IntegrationConnector
    PlatformName="Twitch"          @* Full name, used in aria-label *@
    ShortName="TWI"                @* 3-char uppercase abbreviation shown in pill *@
    Color="#9146FF"                @* Brand hex, drives --plat-color CSS var *@
    ConnectionState="_twitchState" @* IntegrationConnectionState enum *@
    IsLive="_twitchIsLive"         @* Optional bool, renders compact LIVE badge *@
    ViewerCount="@null"            @* Optional int?, shown only when Connected *@
    OnConnectClicked="OpenDialog" />
```

### IntegrationAuthDialog
```razor
<IntegrationAuthDialog
    PlatformName="Twitch"
    Color="#9146FF"
    AuthorizationUrl="@TwitchTokenManager.GetAuthorizationUrl()"
    IsOpen="_dialogOpen"
    IsConnected="@(_twitchState == IntegrationConnectionState.Connected)"
    ErrorMessage="@_errorMsg"      @* null = no error shown *@
    OnClose="CloseDialog"
    OnConnected="HandleConnected"
    OnDisconnect="HandleDisconnect" />
```

## State Mapping Pattern (TopBar)

```csharp
// Subscribe to live state changes on init
TwitchService.ConnectionStateChanged += OnTwitchStateChanged;
TwitchService.StreamLiveStateChanged += OnTwitchStreamLiveStateChanged;
await TwitchService.RefreshConnectionState(_cts.Token);
_twitchState = MapTwitchState(TwitchService.ConnectionState);
_twitchIsLive = TwitchService.IsStreamLive;

// Map platform-specific enum → generic IntegrationConnectionState
private static IntegrationConnectionState MapTwitchState(TwitchConnectionState state) => state switch
{
    TwitchConnectionState.Connected => IntegrationConnectionState.Connected,
    TwitchConnectionState.Connecting => IntegrationConnectionState.Connecting,
    TwitchConnectionState.Authorized or TwitchConnectionState.Disconnected => IntegrationConnectionState.NotConnected,
    TwitchConnectionState.NotAuthorized => IntegrationConnectionState.NotConnected,
    TwitchConnectionState.Error => IntegrationConnectionState.Error,
    _ => IntegrationConnectionState.Unknown
};
```

### Minimal Twitch live-state seam

When a platform only needs a live/offline badge, keep the service contract small:

```csharp
public interface ITwitchService : IChatSource
{
    bool IsStreamLive { get; }
    event EventHandler<bool>? StreamLiveStateChanged;
    Task RefreshStreamState(CancellationToken cancellationToken = default);
}
```

For Twitch, back this with Helix `GET /helix/streams?user_id={BroadcasterId}` and keep `BroadcasterId` in `TwitchOptions`.

## Auth / Management Flow

1. **Not Connected → Connect:**
   - User taps "Not Connected" chip → `OpenTwitchDialog()` sets `_twitchDialogOpen = true`
   - `IntegrationAuthDialog` renders as a fixed-position modal overlay in "auth mode" (`IsConnected=false`)
   - User taps "Authorize with Twitch" → dialog opens a new tab with `TwitchTokenManager.GetAuthorizationUrl()`
   - User completes OAuth in the new tab, then clicks "Done — I'm Connected"
   - `HandleConnected()` closes dialog and refreshes connection state
   - Connection state event fires on success → chip updates automatically

2. **Connected → Manage/Disconnect:**
   - User taps connected TWI chip → `OpenTwitchDialog()` sets `_twitchDialogOpen = true`
   - `IntegrationAuthDialog` renders in "manage mode" (`IsConnected=true`)
   - Dialog shows "Twitch is connected and authorized. You can disconnect to revoke access or re-authorize with a different account."
   - User taps "Disconnect" → `HandleDisconnect()` calls `TwitchTokenManager.Revoke()` then refreshes connection state
   - Dialog closes; chip updates to NotConnected state

## Extending to Other Platforms

To add a new integration (e.g., YouTube):
1. Create platform-specific `ConnectionState` → `IntegrationConnectionState` mapper
2. Add `<IntegrationConnector>` + `<IntegrationAuthDialog>` pair in TopBar
3. Subscribe to the platform service's state-changed event on init
4. Implement `ITokenManager` equivalent for the platform's auth URL and `Revoke()` method
5. Wire `OnDisconnect` callback to call platform token manager's `Revoke()` method

**Example disconnect handler:**
```csharp
private async Task HandleYouTubeDisconnect()
{
    try
    {
        await YouTubeTokenManager.Revoke(_cts.Token);
        await YouTubeService.RefreshConnectionState(_cts.Token);
        _youtubeDialogOpen = false;
    }
    catch
    {
        _youtubeAuthError = "Failed to disconnect. Please try again.";
    }
}
```

### Planned-but-disabled platforms

When a platform should be visible in the operator UI but must not be clickable yet, reuse `IntegrationConnector` instead of adding a separate placeholder component:

```razor
<IntegrationConnector
    PlatformName="LinkedIn"
    ShortName="LI"
    Color="#0A66C2"
    ConnectionState="IntegrationConnectionState.Unknown"
    IsAvailable="false"
    UnavailableLabel="Pending"
    UnavailableReason="LinkedIn integration is intentionally unavailable until LinkedIn Live API access is approved." />
```

Use a short visible status label (`Soon`, `Pending`) and put the fuller explanation in `UnavailableReason` for tooltip and accessibility text.

## Visual States

| State         | Visual                                          | Interactive |
|---------------|------------------------------------------------|-------------|
| Unknown       | Dim dot + platform name                        | No |
| NotConnected  | Amber dot + "Not Connected" label, glow border  | Yes (opens dialog in auth mode) |
| Connecting    | Pulsing colored dot + "…" label                | No |
| Connected     | Live colored dot + name + optional LIVE badge / viewer count | **Yes (opens dialog in manage mode)** |
| Error         | Red dot + "Error — Retry" label, glow border   | Yes (opens dialog in auth mode) |
| Disabled      | Muted chip + short `Soon` / `Pending` label    | No |

**Connected-state interaction (new in 2026-05-29):**
- Connected chips are now clickable buttons, not static spans
- Clicking opens the dialog in "manage mode" showing connection status and offering Disconnect
- Prevents "dead interaction" when user already has a stored token
- Disconnect button styled as destructive action (red/danger theme)

## Touch UX Notes

- All interactive elements have `min-height: 44px; min-width: 44px` touch targets
- Disabled chips keep the same footprint so the header layout stays stable and touch-safe
- Backdrop click dismisses the dialog (same as Cancel)
- No hover-only affordances — all states are touch-safe
- **All states (NotConnected, Connected, Error) render as clickable buttons** — no dead interaction zones
- Connected state opens dialog in "manage mode" for safe disconnect/revoke flow

## CSS Variable Dependencies

Uses standard theme variables: `--glass-bg`, `--glass-border`, `--color-text`, `--color-text-muted`, `--font-size-xs`, `--font-size-sm`, `--spacing-sm`, `--radius-btn`, `--duration-fast`.
Platform brand color is passed via `--plat-color` CSS custom property.
