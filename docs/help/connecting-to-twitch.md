# Connecting Thiccdal to Twitch

This guide explains how to set up and connect Thiccdal to your Twitch account so the control system can chat, receive events, and manage your stream.

## Prerequisites: Twitch Developer Credentials

**Before you can connect Thiccdal to Twitch, you must obtain a Client ID and Client Secret from the Twitch Developer Console and configure them in `appsettings.json`.**

> **ℹ️ Current State**: This is a temporary step. Once a first-run setup UI is added, you'll be able to provide these credentials through Thiccdal's UI instead of editing config files manually.

### If You Haven't Set Up Credentials Yet

See the **[Getting Started: Twitch Integration](./getting-started.md#twitch-integration)** section for detailed, step-by-step instructions on:

1. Creating a Twitch Developer application
2. Retrieving your Client ID and Client Secret
3. Configuring `appsettings.json` with these values

Once `ClientId` and `ClientSecret` are filled in `appsettings.json` and Thiccdal has started, return to this guide to complete the in-app connection flow.

---

Thiccdal uses **OAuth 2.0** — a secure, industry-standard login flow — to connect to Twitch. When you authenticate, you grant Thiccdal specific permissions (called "scopes") to read chat, send messages, and receive follower events. Your Twitch credentials are never stored; instead, Thiccdal stores a secure access token that Twitch issues.

The complete Twitch setup (channel configuration, authorization, and IRC connection) is handled through a single dialog from the Integrations page.

## Quick Start

1. Open the Thiccdal control dashboard in your browser
2. Navigate to **Integrations** (or click a Twitch badge in the top bar)
3. Click the **Twitch** platform tile
4. A setup dialog opens with three steps:
   - **Target Channel**: Configure which channel the bot should join
   - **Bot Authorization**: Authorize your bot account with Twitch OAuth
   - **IRC Connection**: Connect the bot to Twitch chat
5. Complete each step in order
6. Once connected, you can send test messages and verify chat activity in the dialog
7. Close the dialog when setup is complete

## What You'll See

### Before Connection

- The Twitch indicator badge shows as **inactive** (grayed out or disconnected icon)
- A tooltip or label says "Not Connected"

### During Connection

- The indicator changes to a **connecting state** (may show a spinner or animated icon)
- This typically lasts 1–3 seconds while Thiccdal exchanges your authorization code for a token

### After Connection

- The Twitch indicator badge lights up in **Twitch purple (#9146FF)**
- The indicator shows **"Connected"** when you hover over it
- Chat and platform events (follows, subscriptions, raids, etc.) now flow through Thiccdal

## The Setup Flow

### Step 1: Open Twitch Setup Dialog

From the **Integrations** page, click the **Twitch** platform tile. A setup dialog opens with all configuration steps.

### Step 2: Configure Target Channel

In the first section of the dialog:

1. Enter the **channel name** (broadcaster login) the bot should join
   - Enter just the name without # prefix (e.g., `your_channel_name`)
2. Optionally expand **Advanced** to enter the broadcaster user ID for EventSub features
3. Click **Save Target Channel**
4. Wait for confirmation that the channel was saved

**Note**: The bot account and target channel can be different. For example, the bot might sign in as `my_bot_account` but join the chat of `my_main_channel`.

### Step 3: Authorize Bot Account

In the second section of the dialog:

1. Click **Authorize with Twitch**
2. A new browser tab opens showing Twitch's OAuth login page
3. Log in with the **bot account** (if not already logged in)
4. Review the requested permissions:
   - `user:read:chat` and `user:write:chat` — Read and send chat messages
   - `user:bot` and `channel:bot` — Act as a bot in the channel
   - `moderator:read:followers` — Read follower events (requires moderator status)
   - `channel:read:subscriptions`, `bits:read`, and `channel:read:redemptions` — Read sub, cheer, and channel point events
5. Click **Authorize** on the Twitch page
6. You'll be redirected back to Thiccdal
7. The dialog shows "Authorized" status once complete

### Step 4: Connect to IRC

In the third section of the dialog:

1. Ensure you've saved a target channel and authorized the bot (steps 2 and 3)
2. Click **Connect to #your_channel_name**
3. Wait for the connection to establish (usually 1-3 seconds)
4. Once connected, recent chat messages appear in the dialog

### Step 5: Test the Connection (Optional)

When connected, the dialog shows a **Recent Chat** section:

- View recent messages from your channel
- Send a test message using the composer at the bottom
- Verify the bot is joined to the correct channel

## How Long Does the Token Last?

- **Access tokens** last for about 4 hours
- **Refresh tokens** allow Thiccdal to obtain a new access token without you needing to log in again
- **Automatic refresh**: Thiccdal automatically refreshes the token when it approaches expiration — you won't see any action on your part

## Checking Your Connection Status

### In the Control Dashboard

Look at the top-left platform indicators:

- **Purple badge (TWI)**: If lit and steady, Twitch is **connected**
- **Purple badge (TWI)**: If grayed out, Twitch is **not connected** — click to log in
- **Purple badge with exclamation/error icon**: A connection error has occurred — see [Troubleshooting](#troubleshooting)

### Via Browser Console (Advanced)

If you open the browser's developer console (F12 → Console tab), you may see log entries like:

```
[INFO] Twitch connection established
[WARN] Twitch token expired; refreshing
[ERROR] Twitch reconnection failed
```

## Disconnecting or Re-authenticating

### To Disconnect from Chat Only

1. Open the Twitch setup dialog from the Integrations page
2. Scroll to step 3 (IRC Connection)
3. Click **Disconnect from Chat**
4. The bot leaves the channel but authorization remains

### To Remove Authorization

1. Open the Twitch setup dialog from the Integrations page
2. Scroll to step 2 (Bot Authorization)
3. Click **Disconnect**
4. The bot is disconnected from chat (if connected) and authorization is revoked
5. Your stored token is deleted locally

After removing authorization:

- The Twitch indicator returns to "Not Authorized" status
- No chat or events will flow from Twitch until you re-authorize

### To Switch Bot Accounts

1. Open the Twitch setup dialog
2. In step 2, click **Disconnect** to remove the current authorization
3. Wait 1–2 seconds
4. Click **Authorize with Twitch** and log in with a different Twitch account
5. Complete the OAuth flow
6. Return to the dialog and proceed with IRC connection (step 3)

## Troubleshooting

### Setup Dialog Doesn't Open

**What's happening**: The dialog may not have loaded properly. Try:

1. Refresh the browser page (F5)
2. Navigate back to the Integrations page
3. Click the Twitch platform tile again

### OAuth Login Window Doesn't Open

**What's happening**: Your browser may have blocked the popup window.

1. Check if your browser showed a **popup-blocked** notification (often in the address bar or tab area)
2. Click it and select **"Allow popups for this site"**
3. In the setup dialog, click **Authorize with Twitch** again

**Note**: The new tab opens when you click the authorize button — the setup dialog remains visible throughout the flow.

### "Authorization Failed" or Error Message

**What's happening**: The authorization didn't complete successfully.

**Possible causes**:

- You clicked "Deny" instead of "Authorize" on the Twitch permission screen → Try again and click "Authorize"
- You closed the authorization tab before completing the flow → Click **Authorize with Twitch** again in the dialog
- The authorization code expired (older than 10 minutes) → Close and reopen the dialog to start fresh
- Twitch's servers are down (rare) → Wait a few minutes and try again

**To recover**:

1. Read the error message displayed in the dialog
2. Close the dialog (click Close or X button)
3. Wait 2–3 seconds
4. Reopen the Twitch setup dialog from Integrations
5. Start a fresh authorization attempt

### "Token Refresh Failed" (Connection Drops)

**What's happening**: Thiccdal tried to refresh your access token and failed.

**Possible causes**:

- Your Internet connection is temporarily down
- Twitch's servers are temporarily unavailable
- Your token was revoked outside of Thiccdal (e.g., you changed your Twitch password)
- Thiccdal has been offline for more than ~30 days and the refresh token has expired

**To recover**:

1. Check your Internet connection — is it working? Can you visit Twitch.tv?
2. Wait 30 seconds and Thiccdal will attempt to reconnect automatically
3. If the error persists, disconnect and re-authorize:
   - Open the Twitch setup dialog from Integrations
   - In step 2, click **Disconnect**
   - Click **Authorize with Twitch** → Complete the OAuth flow

### "Moderator Read Followers" Scope Denied

**What's happening**: You saw the permission request but chose not to grant `moderator:read:followers`.

**Effect**: Thiccdal can still receive and send chat messages, but it won't receive follow events. Follows might not appear in overlays or event trackers.

**To fix**:

- If you're a moderator of your channel and want follower events, disconnect and re-authenticate, making sure to approve all permissions
- If you're not a moderator, this scope cannot be granted; follower events are unavailable

### "No Token Found" Error When Thiccdal Starts

**What's happening**: Thiccdal expected a stored token but couldn't find one.

**Possible causes**:

- This is the first time Thiccdal has started and needs authentication
- The database was reset or cleared
- The Thiccdal configuration changed

**To fix**:

1. Open the control dashboard
2. Navigate to the Integrations page
3. Click the Twitch platform tile to open the setup dialog
4. Complete steps 1-3 in the dialog (channel, authorization, connection)

## Permission Scope Reference

| Scope | What It Does | Why Thiccdal Needs It |
|-------|--------------|----------------------|
| `user:read:chat` | Read chat messages | Receive chat from viewers and monitor it for bot commands |
| `user:write:chat` | Send chat messages | Reply to chat on your behalf |
| `user:bot` | Act as a bot user | Required alongside `user:read:chat` for chat events |
| `channel:bot` | Act as a bot in the channel | Lets the bot account operate in the broadcaster's channel |
| `moderator:read:followers` | Read follower events | Display follows in overlays and event trackers (requires moderator status) |
| `channel:read:subscriptions` | Read subscription events | Display subs and gift subs |
| `bits:read` | Read cheer events | Display bits and cheers |
| `channel:read:redemptions` | Read channel point redemptions | Trigger overlays and bot responses from redeems |

Raid events (`channel.raid`) need no scope — Twitch delivers them on the strength of the
broadcaster condition alone.

## Security & Privacy

- **Your credentials are never stored**: Only a secure access token is kept in Thiccdal's database
- **Token encryption** (if available in your deployment): The token may be encrypted at rest
- **Automatic revocation**: If you disconnect Thiccdal from the control UI, the token is immediately revoked at Twitch and deleted locally
- **Token expiration**: Tokens expire automatically and are refreshed only when needed
- **Read-only for most scopes**: Thiccdal requests minimal permissions — it can only read chat and follow events, not modify your account

## What's Next?

Once Thiccdal is fully connected to Twitch (all three steps complete):

- **Chat integration** is active: Thiccdal can read messages and display them in overlays or the teleprompter
- **Event tracking** begins: Follows, subscriptions, raids, and other events are logged
- **Bot commands** respond to chat (if configured)
- **Stream info** (title, game, viewer count) is available for status overlays
- **Test messaging** is available directly from the setup dialog

You can reopen the Twitch setup dialog anytime to:

- Change the target channel
- Disconnect from chat temporarily
- View recent chat activity
- Send test messages
- Remove authorization entirely

For more information, see:

- [Configuring Chat Settings](./configuring-chat.md) (if available)
- [Using Overlays](./using-overlays.md) (if available)
- [Bot Commands](./bot-commands.md) (if available)

---

## Still Have Questions?

If you encounter an issue not covered here:

1. Check the browser console (F12) for error messages — they often provide clues
2. Look at your Twitch account's **Connected Apps** (Twitch Settings → Connections) to verify Thiccdal appears there
3. Disconnect Thiccdal, wait 10 seconds, and re-authenticate
4. Restart Thiccdal entirely and try again

If problems persist, contact your Thiccdal administrator or support team with a screenshot of the error and any console messages.
