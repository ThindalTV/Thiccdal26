# Connecting Thiccdal to Facebook Live

This guide explains how to set up and connect Thiccdal to your Facebook Page for Facebook Live streaming integration.

> **⚠️ Current Status**: Facebook Live integration is implemented for live-video creation, title/description updates, comment polling, reaction polling, and outbound comment posting. It still requires manual Page token configuration, uses polling rather than push delivery, and does **not** emit follower events because that requires separate Facebook webhook wiring.

---

## Overview

Thiccdal allows you to:
- ✅ Create a Facebook `LiveVideo` and return the RTMPS ingest URL
- ✅ Stop the active `LiveVideo` cleanly
- ✅ Monitor Facebook Live comments (via polling)
- ✅ Post operator chat messages back to the active live video (requires `pages_manage_engagement`)
- ✅ Receive normalized comment and reaction events in the unified event feed
- ✅ Update the active live video's title and description mid-stream
- ❌ No real-time push comment stream (uses polling instead)
- ❌ No follower events in this phase (blocked on Page webhook subscriptions)
- ❌ Category updates are not available for Facebook Live videos

---

## Prerequisites

To connect Facebook Live to Thiccdal, you will need:

### Your Facebook Page
- A **Facebook Page** (not a personal profile) with streaming enabled
- **Admin or Editor access** to the page
- Streaming privileges enabled on the page (if streaming is restricted in your region)

### Developer Application
- A **Facebook Developer account** (create at [developers.facebook.com](https://developers.facebook.com))
- A **Facebook App** registered on your account with the following permissions:
  - **Pages API** (to manage your page)
  - **Live Video API** (to stream to your page)
  - **Stream Ingestion API** (optional, for RTMP ingestion)

### OAuth Credentials
- Your app's **App ID**
- Your app's **App Secret** (keep this private)
- A **Page Access Token** (granted via OAuth)
- A valid **OAuth Redirect URL** (e.g., `https://your-thiccdal-server/auth/facebook/callback`)

---

## Getting Started (When Available)

### Step 1: Create a Facebook Developer App

1. Go to [developers.facebook.com](https://developers.facebook.com)
2. Log in with your Facebook account
3. Click **My Apps** → **Create App**
4. Choose **For Consumers** (or the appropriate category for your use case)
5. Fill in:
   - **App Name**: e.g., "Thiccdal Facebook Live"
   - **App Contact Email**: Your email
   - **App Purpose**: "Stream management / broadcasting tools" (or similar)
6. Click **Create App**

### Step 2: Configure Your App

1. In the app dashboard, go to **Settings** → **Basic**
2. Note your **App ID** and **App Secret**
3. Scroll to **App Domains** and add:
   - Your Thiccdal server domain (e.g., `your-thiccdal-server.com`)
4. Go to **Settings** → **Advanced**
5. Under **Redirect URIs**, add:
   ```
   https://your-thiccdal-server/auth/facebook/callback
   ```
6. Click **Save Changes**

### Step 3: Enable Required APIs

1. In the app dashboard, go to **Products**
2. Add the following products to your app:
   - **Facebook Login** (for OAuth)
   - **Pages API** (to read/write page data)
   - **Live Video API** (to manage live videos)
3. For each, configure required permissions

### Step 4: Get a Page Access Token

1. Go to **Tools** → **Graph API Explorer**
2. Select your app from the dropdown at the top
3. In the left sidebar, select **Page** as the node type
4. Search for and select your Facebook Page
5. In the graph explorer, type:
   ```
   [YOUR_PAGE_ID]/access_tokens
   ```
6. Click **Get Token**
7. Copy the resulting **Page Access Token** (starts with `EAAB...`)
   - **Keep this token secret** — anyone with it can post to your page

### Step 5: Configure Thiccdal

Edit your `appsettings.json` to add:

```json
{
  "Facebook": {
    "AppId": "your-app-id",
    "AppSecret": "your-app-secret",
    "PageId": "your-page-id",
    "PageAccessToken": "your-page-access-token",
    "GraphApiVersion": "v21.0",
    "DefaultPrivacy": "EVERYONE",
    "PollIntervalMs": 5000
  }
}
```

**Or use environment variables** (preferred for production):

```bash
export FACEBOOK_APP_ID=your-app-id
export FACEBOOK_APP_SECRET=your-app-secret
export FACEBOOK_PAGE_ID=your-page-id
export FACEBOOK_PAGE_ACCESS_TOKEN=your-page-access-token
```

### Step 6: Authorize and Connect

1. Start Thiccdal
2. In the operator dashboard, navigate to **Integrations**
3. Click the **Facebook (FB)** platform tile
4. Configure the Facebook section with a valid Page token and Page ID
5. Start relay from the operator UI (or the integration surface that creates the active `LiveVideo`)
6. Begin streaming — Thiccdal will monitor your Facebook Live feed

---

## Streaming to Facebook

Once connected, streaming to Facebook is handled by Thiccdal's **multicast RTMP fanout**:

1. **Configure your streaming source** (OBS, StreamYard, etc.)
   - Point to Thiccdal's local RTMP ingest URL (displayed in the Integrations page)
   - Use the stream key provided by Thiccdal

2. **Start streaming**
   - Thiccdal automatically ingests your stream and fans it out to all connected platforms, including Facebook

3. **Stream appears on Facebook**
   - Your broadcast goes live on your Facebook Page
   - Chat from Facebook viewers aggregates into Thiccdal's unified chat feed

---

## Facebook Requirements & Restrictions

### Account & Page Requirements

| Requirement | Details |
|---|---|
| **Facebook Account** | Personal account to manage the page |
| **Facebook Page** | Must be a Page (not a personal profile) |
| **Page Role** | Admin or Editor access required |
| **Streaming Enabled** | Page must have live streaming capability |

### Geographic Restrictions

Some regions restrict live streaming on Facebook Pages:
- Your Facebook page's primary country is used to determine eligibility
- If your page is restricted, contact Facebook Support to request streaming access

### Streaming Quality & Bitrate

Facebook Live accepts RTMP streams with these specifications:

| Setting | Recommended | Maximum |
|---|---|---|
| Resolution | 1920×1080 (1080p) | 4096×2160 (4K) |
| Frame Rate | 30 fps | 60 fps |
| Bitrate | 4–8 Mbps | 35 Mbps |
| Codec | H.264 video, AAC audio | H.264, H.265 (HEVC) |

**Note**: Facebook may transcode your stream to multiple bitrates for different viewer connections.

### Chat Features & Limits

| Feature | Limitation |
|---|---|
| **Message Length** | 500 characters max |
| **Rate Limiting** | ~60 messages per minute per user |
| **Moderation** | Use Facebook's native moderation tools in parallel with Thiccdal |
| **Follower Events** | Not surfaced in this phase; Facebook webhook wiring is still required |
| **Category Updates** | Not supported by Facebook Live video API |
| **Emotes** | Facebook emoji and custom reactions (not Twitch/custom emotes) |

### Viewer Insights

Thiccdal will track and display:
- Live viewer count (polled every 5–10 seconds)
- Total watch time
- Reactions (like, love, haha, wow, sad, angry)
- Shares
- Comments

---

## Troubleshooting (When Available)

### "Authorization Failed" or Error Message

**Possible causes:**
- Your Facebook App ID or App Secret is incorrect
- The redirect URL doesn't match your configuration
- Your Page Access Token has expired or been revoked

**To fix:**
1. Verify your credentials in `appsettings.json`
2. Check that the redirect URI matches exactly in both Facebook Dev Console and Thiccdal config
3. Generate a fresh Page Access Token if the current one is expired
4. Restart Thiccdal

### "No Live Video Found" / "Cannot Connect to Stream"

**Possible causes:**
- Your Facebook Live broadcast is not active or has ended
- Thiccdal does not have the correct Page ID
- The streaming key or RTMP URL is incorrect

**To fix:**
1. Verify the Facebook Live video is currently live on your page
2. Check the Page ID in your configuration
3. Check Thiccdal logs for streaming errors
4. Restart the broadcast and reconnect

### "Chat Not Appearing"

**Possible causes:**
- Facebook Live chat is disabled for your broadcast
- Your broadcast's chat moderation is set to "Hold all messages"
- Thiccdal has insufficient permissions (missing `Pages` scope)

**To fix:**
1. In Facebook Live settings, ensure **Comments** are enabled
2. Check moderation settings — "Hold all messages" will not show in Thiccdal
3. Re-authorize Thiccdal to ensure all scopes are granted
4. Wait 10–15 seconds for chat polling to catch up

---

## Security & Permissions

- **App Secret**: Treat it like a password. Never share or commit it to version control.
- **Page Access Token**: Store in environment variables, not `appsettings.json` (for production).
- **OAuth Redirect URL**: Must use HTTPS in production (HTTP is acceptable for localhost).
- **Scopes Requested**: `pages_manage_metadata`, `pages_read_engagement`, `pages_read_user_content`, `pages_manage_engagement`

---

## Rate Limits & Quotas

Facebook imposes API rate limits. Thiccdal will respect these limits by:
- Polling live chat every **5 seconds** (configurable)
- Batching operations where possible
- Backing off if rate limits are exceeded

If you see rate limit errors:
1. Increase `LiveVideoPollingIntervalSeconds` to 10+ seconds
2. Contact Facebook Developer Support to request a quota increase

---

## What's Next?

Once Facebook Live is integrated:

1. **Multi-Platform Dashboard**: View unified chat from Twitch, YouTube, Discord, and Facebook simultaneously
2. **Platform-Specific Overlay Filters**: Display only Facebook chat, or aggregate all platforms
3. **Event Tracking**: See Facebook follows, reactions, and shares in your event dashboard
4. **Bot Commands**: Respond to Facebook chat with the same commands you use for Twitch/YouTube
5. **Stream Insights**: View viewer counts, engagement metrics, and retention across all platforms

---

## Status & Timeline

- **Current**: Under development (placeholder code in repository)
- **Target Availability**: TBD (follow [GitHub releases](https://github.com/ThindalTV/Thiccdal26/releases) for updates)
- **Testing**: We encourage beta testers! Open an issue if you're interested in early access

---

## Support

For questions or issues:
- **GitHub Issues**: [Report a bug or request a feature](https://github.com/ThindalTV/Thiccdal26/issues)
- **Documentation**: Check the [main docs](../README.md) for system overview
- **Architecture**: See `/docs/architecture/` for technical details

---

**Note**: This document describes planned functionality. Implementation details may change before release. Check back for updates as the Facebook Live integration progresses.
