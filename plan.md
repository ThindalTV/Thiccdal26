# Twitch Setup Dialog Migration Plan

## Current State Analysis

**TwitchAuthDialog** (simple)
- Shows connection status badge
- Authorize button → navigates to Twitch OAuth
- Disconnect button → revokes token
- Used by Integrations page and TopBar

**TwitchConnect page** (/twitch/connect) - full setup workflow
- Step 1: Target channel configuration
  - Input for target channel name
  - Input for broadcaster ID (advanced)
  - Save button
- Step 2: Bot authorization
  - Authorize button → Twitch OAuth
  - Disconnect button → revoke
- Step 3: IRC connection
  - Connect button (requires channel + auth)
  - Disconnect button
  - Chat message viewer (when connected)
  - Quick test message composer

**OAuth Callback**
- Currently redirects to /dashboard (per previous pass)
- Supports query params for errors

## Migration Strategy

### 1. Enhanced TwitchAuthDialog → TwitchSetupDialog
Transform the simple auth dialog into a full setup dialog by:
- Renaming TwitchAuthDialog → TwitchSetupDialog
- Adding target channel configuration section
- Adding IRC connection control section
- Adding optional chat preview section (when connected)
- Reusing all logic from TwitchConnect.razor

### 2. Update Integrations Page
- Reference TwitchSetupDialog instead of TwitchAuthDialog
- Keep same trigger (click Twitch badge)

### 3. TwitchConnect Page Options
Option A: Delete entirely
Option B: Redirect to /integrations or /dashboard
Option C: Keep as compatibility shim with redirect notice

**Decision: Option B** - Redirect to /integrations with preserved error query params

### 4. OAuth Callback
- Keep redirect to /dashboard (works for dialog flow)
- Errors are handled via query params

### 5. Documentation Updates
- Update connecting-to-twitch.md to reflect dialog-based setup
- Remove references to /twitch/connect page
- Add details about channel config and IRC connection in dialog

### 6. Test Updates
- Update RouteRenderingTests to expect redirect from /twitch/connect
- Add component tests for TwitchSetupDialog

## Implementation Order

1. Create TwitchSetupDialog.razor (copy/merge TwitchAuthDialog + TwitchConnect logic)
2. Update Integrations.razor to use TwitchSetupDialog
3. Convert TwitchConnect.razor to redirect shim
4. Update documentation
5. Update tests
6. Build and validate

## Files to Change

- src/Thiccdal/Components/Admin/TwitchAuthDialog.razor → TwitchSetupDialog.razor
- src/Thiccdal/Components/Pages/Integrations.razor
- src/Thiccdal/Components/Pages/TwitchConnect.razor (convert to redirect)
- docs/help/connecting-to-twitch.md
- src/Tests/Thiccdal.Tests/RouteRenderingTests.cs

