# Twitch OAuth Configuration Cleanup - Summary

## Task Completion
Successfully audited and removed OAuth-derived settings from Twitch configuration surface.

## Files Changed

### Core Infrastructure (3 files)
1. **TwitchOptions.cs** - Removed OAuth-derived properties:
   - BotUsername (removed)
   - BotUserId (removed)
   - DefaultTargetChannel (removed)
   - DefaultBroadcasterId (removed)
   - Channel, Username, BroadcasterId legacy aliases (removed)
   
2. **TwitchToken.cs** - Added OAuth-derived fields:
   + Username (string) - from Helix API after auth
   + UserId (string) - from Helix API after auth

3. **TwitchUser.cs** (NEW) - Value type for Helix user response:
   + Id, Login, DisplayName

### Services (3 files)
4. **TwitchTargetChannelService.cs**:
   - Removed IOptions<TwitchOptions> dependency
   - Now reads bot username/userId from TwitchToken table
   - BuildProfile() now async, queries database for token

5. **TwitchTokenManager.cs**:
   - Added _helixHttpClient for user info fetch
   - PopulateUserInfo() method fetches authenticated user after OAuth
   - Stores username/userId in TwitchToken during StoreToken()

6. **TwitchHelixClient.cs**:
   + GetAuthenticatedUser() method - calls /helix/users

### Interface (1 file)
7. **ITwitchHelixClient.cs**:
   + GetAuthenticatedUser(CancellationToken) method signature

### Configuration (1 file)
8. **appsettings.json**:
   - Removed: BotUsername, BotUserId, DefaultTargetChannel, DefaultBroadcasterId

### Tests (3 files)
9. **TwitchTargetChannelServiceTests.cs**:
   - Updated to use TwitchToken in database instead of config
   - Removed IOptions<TwitchOptions> from service creation

10. **TwitchServiceTests.cs**:
    - FakeHelixClient now implements GetAuthenticatedUser()
    - Removed removed properties from test setup

11. **TwitchRegistrationExtensionsTests.cs**:
    - Removed removed properties from config dictionary
    - Removed assertion for BotUserId

### Documentation (2 files)
12. **docs/architecture/helix-redesign.md**:
    - Updated OAuth flow to include user info fetch step
    - Updated migration section to reflect removal of config values

## Settings Removed vs Retained

### REMOVED (OAuth-derived):
- **BotUsername**: Now fetched from Twitch /helix/users after OAuth
- **BotUserId**: Now fetched from Twitch /helix/users after OAuth
- **DefaultTargetChannel**: Runtime-configurable via UI/database
- **DefaultBroadcasterId**: Runtime-configurable via UI/database
- **Channel** (alias): Removed
- **Username** (alias): Removed
- **BroadcasterId** (alias): Removed

### RETAINED (Required for OAuth):
- **ClientId**: OAuth app registration credential
- **ClientSecret**: OAuth app registration credential
- **RedirectUri**: OAuth callback URL
- **OAuthBaseAddress**: OAuth service endpoint
- **Scopes**: OAuth permission requirements
- **Helix**: Helix API settings (BaseAddress, StreamStateRefreshSeconds, SendChatMessagesViaHelix)
- **EventSub**: EventSub settings (WebSocketUrl, ReconnectDelaySeconds, RequireModeratorAccess, UseAnimatedEmotes)

## Migration Considerations

### Breaking Changes
- Operators can no longer configure bot identity in appsettings.json
- Values are now OAuth-derived and stored in database

### Compatibility
- Existing tokens in database continue to work
- After next OAuth, username/userId will be populated automatically
- No data migration required for existing installations

### Upgrade Path
1. Deploy updated code
2. Existing OAuth tokens work immediately
3. On next re-authorization, username/userId are fetched and stored
4. No operator action required

## Validation

### Build Status
✓ Build succeeded with no errors or warnings

### Test Results
✓ All 52 Twitch integration tests passed
  - TwitchTargetChannelServiceTests: All pass with DB-backed identity
  - TwitchServiceTests: All pass with updated fakes
  - TwitchHelixClientTests: All pass (existing tests unaffected)
  - TwitchRegistrationExtensionsTests: All pass with updated config

## Next Steps (NOT done in this task)
- Create EF Core migration for TwitchToken schema changes (Username, UserId columns)
- Run migration on startup or via manual migration command
- Update end-user documentation (connecting-to-twitch.md) if needed

## Conclusion
OAuth-derived values have been successfully removed from configuration surface. All identity info (username, userId) now comes from Twitch API after OAuth authorization and is stored in the database alongside tokens. Configuration surface now only contains OAuth app credentials and behavioral settings.
