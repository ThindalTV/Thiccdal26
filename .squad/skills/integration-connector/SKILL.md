# SKILL: Reusable Integration Connector Component Pattern

## What It Is

A pattern for building reusable admin-side integration connection controls in Blazor Server. Provides a touch-friendly chip/pill in the TopBar that shows real-time connection state and opens an auth dialog when not connected.

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
    IsOpen="_dialogOpen"
    ErrorMessage="@_errorMsg"      @* null = no error shown *@
    OnClose="CloseDialog"
    OnAuthorize="DoAuth" />
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

## Auth Flow

1. User taps "Not Connected" chip → `OpenTwitchDialog()` sets `_twitchDialogOpen = true`
2. `IntegrationAuthDialog` renders as a fixed-position modal overlay
3. User taps "Authorize with Twitch" → `AuthorizeTwitch()` calls `TwitchTokenManager.GetAuthorizationUrl()` and `Navigation.NavigateTo(url, forceLoad: true)`
4. OAuth redirect returns to existing handler page (`/twitch/connect`)
5. Connection state event fires on success → chip updates automatically

## Extending to Other Platforms

To add a new integration (e.g., YouTube):
1. Create platform-specific `ConnectionState` → `IntegrationConnectionState` mapper
2. Add `<IntegrationConnector>` + `<IntegrationAuthDialog>` pair in TopBar
3. Subscribe to the platform service's state-changed event on init
4. Implement `ITokenManager` equivalent for the platform's auth URL

## Visual States

| State         | Visual                                          | Interactive |
|---------------|------------------------------------------------|-------------|
| Unknown       | Dim dot + platform name                        | No          |
| NotConnected  | Amber dot + "Not Connected" label, glow border  | Yes (opens dialog) |
| Connecting    | Pulsing colored dot + "…" label                | No          |
| Connected     | Live colored dot + name + optional LIVE badge / viewer count | No          |
| Error         | Red dot + "Error — Retry" label, glow border   | Yes (opens dialog) |

## Touch UX Notes

- All interactive elements have `min-height: 44px; min-width: 44px` touch targets
- Backdrop click dismisses the dialog (same as Cancel)
- No hover-only affordances — all states are touch-safe
- The entire chip is the tap target when not connected (button wraps all contents)

## CSS Variable Dependencies

Uses standard theme variables: `--glass-bg`, `--glass-border`, `--color-text`, `--color-text-muted`, `--font-size-xs`, `--font-size-sm`, `--spacing-sm`, `--radius-btn`, `--duration-fast`.
Platform brand color is passed via `--plat-color` CSS custom property.
