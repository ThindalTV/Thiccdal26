# Connecting to YouTube

This guide shows you how to connect Thiccdal to your YouTube channel so it can read live chat and manage your streams.

---

## Prerequisites

Before connecting YouTube, you need:

- **A YouTube channel** with live streaming enabled
- **A Google Cloud project** with the YouTube Data API v3 enabled
- **OAuth credentials** configured in Google Cloud Console

---

## Step 1: Create a Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Click **Create Project**
3. Name it (e.g., "Thiccdal YouTube Integration")
4. Click **Create**

---

## Step 2: Enable the YouTube Data API v3

1. In your Google Cloud project, go to **APIs & Services → Library**
2. Search for **YouTube Data API v3**
3. Click **Enable**

---

## Step 3: Create OAuth Credentials

1. Go to **APIs & Services → Credentials**
2. Click **Create Credentials → OAuth client ID**
3. If prompted, configure the consent screen:
   - User Type: **External**
   - App name: **Thiccdal**
   - Add your email as support email
   - Add scopes: `youtube.readonly`, `youtube.force-ssl`
4. Choose **Web application** as the application type
5. Name it (e.g., "Thiccdal YouTube Client")
6. Under **Authorized redirect URIs**, add:
   ```
   https://localhost:7148/auth/youtube/callback
   ```
   (Replace `https://localhost:7148` with your Thiccdal server's base URL)
7. Click **Create**
8. **Copy the Client ID and Client Secret** — you'll need these next

---

## Step 4: Configure Thiccdal

Add your YouTube OAuth credentials to `appsettings.json` (or user secrets):

```json
{
  "YouTube": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret",
    "RedirectUri": "https://localhost:7148/auth/youtube/callback",
    "DefaultChannelId": "your-channel-id"
  }
}
```

**Finding your Channel ID:**

1. Go to [YouTube Studio](https://studio.youtube.com)
2. Click **Customization → Basic info**
3. Your channel ID is shown at the bottom

---

## Step 5: Authorize Thiccdal

1. Start Thiccdal
2. In the operator UI, find the **YouTube (YT)** integration chip in the top bar
3. Click it
4. A browser tab will open asking you to sign in to Google
5. Grant Thiccdal access to your YouTube account
6. Return to the operator UI — the YouTube chip should now show **Connected**

---

## Step 6: Go Live on YouTube

1. Start a live stream on YouTube (via OBS, StreamYard, etc.)
2. In Thiccdal, click the **YouTube** chip and choose **Connect**
3. Thiccdal will begin polling your live chat
4. Chat messages and events (Super Chats, memberships) will appear in the chat panel

---

## Troubleshooting

**"Not Authorized" state**
- Verify your Client ID and Client Secret are correct
- Check that your redirect URI matches exactly

**"No active broadcast found"**
- Make sure your YouTube stream is actually live
- Wait 30 seconds for Thiccdal to refresh the broadcast state

**Chat messages not appearing**
- Ensure your broadcast has live chat enabled (not disabled or set to "Hold all messages")
- Check Thiccdal logs for polling errors

---

## OAuth Token Lifecycle

- Tokens are stored in Thiccdal's SQLite database
- Tokens auto-refresh when they expire (valid for 1 hour by default)
- To revoke access, click **Disconnect** in the YouTube integration dialog

---

## Rate Limits

YouTube Data API has quotas. Thiccdal polls live chat every **5 seconds** by default (configurable via `YouTube:LiveChatPollingIntervalSeconds` in `appsettings.json`).

If you hit quota limits:
- Increase the polling interval to 10+ seconds
- Request a quota increase from Google Cloud Console

---

## Next Steps

- See [Bot Commands](./bot-commands.md) for chatbot setup
- See [Overlay Setup](./overlay-setup.md) to display YouTube chat on stream
