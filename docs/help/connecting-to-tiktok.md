# Connecting to TikTok Live

## ⚠️ Status: Awaiting API Approval

Thiccdal has a **complete TikTok Live integration**, but it is **disabled until TikTok approves API access**. Once approval is granted, you'll need to perform a one-time configuration.

---

## Why This Is Blocked

TikTok's streaming APIs are **highly restricted** and not publicly available. To use them, you must:

1. Be a TikTok creator with **Creator Fund eligibility** or **Creator Marketplace partnership**
2. Request special API access through TikTok's Creator Center
3. Have your use case reviewed and approved by TikTok's developer relations team
4. Meet minimum requirements (followers, account age, compliance)

As of now, TikTok has not granted public access to livestream APIs for independent streaming applications. TikTok prioritizes:
- First-party (official) integrations
- Strategic enterprise partnerships
- In-app broadcasting (using TikTok's native interface)

**If you're interested in TikTok integration**, please open a GitHub issue requesting it so we can track demand and contact TikTok's partnership team.

---

## What TikTok Live Offers (Context)

TikTok Live is a powerful feature for creators and influencers:

- **Massive Audience**: TikTok has 1B+ monthly active users
- **Creator Economy**: Direct monetization through gifts and engagement
- **Native Engagement**: Real-time comments, gifts, and follows during stream
- **Discovery**: Streams can go viral through TikTok's algorithm
- **Eligibility**: Requires 1,000+ followers and Creator Fund eligibility (or invitation)

---

## Prerequisites (When TikTok API Is Approved)

To connect TikTok Live to Thiccdal, you would need:

### Your TikTok Account
- A **TikTok Creator Account** (not a personal account)
- At least **1,000 followers** (typical requirement)
- Account must be at least **30 days old**
- **Creator Fund eligible** or invited to livestream
- **Phone number verified** on your account
- **Government ID verification** (TikTok may require this)

### Developer Application
- A **TikTok Developer account** (apply at [developers.tiktok.com](https://developers.tiktok.com))
- An **OAuth application** registered with TikTok
- **API access approval** from TikTok's developer relations team

### OAuth Credentials & Keys
- Your app's **Client Key** (Client ID)
- Your app's **Client Secret** (keep this private)
- A valid **OAuth Redirect URL** (e.g., `https://your-thiccdal-server/auth/tiktok/callback`)
- **Livestream API credentials** (if/when available)

---

## Getting Started (If/When API Is Approved)

### Step 1: Apply for TikTok Developer Account

1. Go to [developers.tiktok.com](https://developers.tiktok.com)
2. Click **Start Building** or **Sign In**
3. Log in with your TikTok creator account
4. Complete the developer registration:
   - Verify your email
   - Provide company/personal information
   - Agree to the Developer Agreement & Policy
5. **Submit for review** (TikTok reviews applications manually)
6. **Await approval** (typically 1–2 weeks)

### Step 2: Create an OAuth Application

Once your developer account is approved:

1. In your TikTok Developer Dashboard, go to **Applications** or **My Applications**
2. Click **Create app**
3. Fill in the application details:
   - **App name**: "Thiccdal Live Streaming"
   - **App category**: "Livestreaming" or "Media"
   - **Description**: Explain that this is a streaming orchestration tool
4. Complete the application review process
5. **Await OAuth app approval** (another review process)

### Step 3: Configure OAuth Settings

Once your OAuth app is approved:

1. In your app's settings, go to **OAuth Configuration** or **Redirect URLs**
2. Add your redirect URL:
   ```
   https://your-thiccdal-server/auth/tiktok/callback
   ```
3. Copy your **Client Key** and **Client Secret**
4. Save your configuration

### Step 4: Request Livestream API Scope

This is critical and may require additional approval:

1. In your app dashboard, look for **API Scopes** or **Permissions**
2. Request the following scopes (if available):
   - `video.upload` (for uploading or starting streams)
   - `user.info.basic` (to read your account)
   - `livestream.info` (to read livestream state)
   - `livestream.manage` (to create/end livestreams)
3. **Submit your scope request** (TikTok will review)
4. **Await approval** (this may take additional time or may be denied)

**⚠️ Note**: TikTok rarely approves third-party livestream API access. You may need to provide additional business justification.

### Step 5: Configure Thiccdal

Once the integration is available and scopes are approved, edit your `appsettings.json` to add:

```json
{
  "TikTok": {
    "ClientKey": "your-client-key",
    "ClientSecret": "your-client-secret",
    "RedirectUri": "https://your-thiccdal-server/auth/tiktok/callback",
    "LiveStreamPollingIntervalSeconds": 5
  }
}
```

**Or use environment variables** (preferred for production):

```bash
export TIKTOK_CLIENT_KEY=your-client-key
export TIKTOK_CLIENT_SECRET=your-client-secret
```

### Step 6: Authorize and Connect

1. Start Thiccdal
2. In the operator dashboard, navigate to **Integrations**
3. Click the **TikTok (TT)** platform tile
4. Complete the authorization flow:
   - **Target Account**: Confirm your TikTok creator account
   - **Authorize**: Log in and grant permissions to Thiccdal
   - **Connect**: Establish the live stream link
5. Begin streaming — Thiccdal will manage your TikTok Live broadcast

---

## Streaming to TikTok

Once connected, streaming to TikTok would work via Thiccdal's **multicast RTMP fanout**:

1. **Configure your streaming source** (OBS, StreamYard, etc.)
   - Point to Thiccdal's local RTMP ingest URL (displayed in the Integrations page)
   - Use the stream key provided by Thiccdal

2. **Start streaming**
   - Thiccdal automatically ingests your stream and fans it out to all connected platforms, including TikTok

3. **Stream appears on TikTok**
   - Your broadcast goes live on your TikTok profile
   - Engagement from TikTok viewers (viewers, gifts, comments) aggregates into Thiccdal's unified dashboard

---

## TikTok Live Requirements & Restrictions (Context)

### Account Requirements

| Requirement | Details |
|---|---|
| **Account Type** | Creator Account (not personal account) |
| **Followers** | At least 1,000 followers (typical) |
| **Account Age** | At least 30 days old |
| **Creator Fund** | Eligible or invited to livestream |
| **Verification** | Phone number verified and government ID verified (may be required) |

### Streaming Specifications

TikTok typically accepts RTMP streams with these specifications:

| Setting | Typical Specs |
|---|---|
| Resolution | 720p–1080p |
| Frame Rate | 30–60 fps |
| Bitrate | 2–8 Mbps |
| Codec | H.264 video, AAC audio |

### Engagement Features

TikTok Live viewers can:
- **Watch the stream** in real-time
- **Send gifts** (can be monetized by creator)
- **Comment** with real-time chat
- **Follow the creator** during the broadcast
- **Share the stream** to their own feeds

### Monetization

Creators can earn from TikTok Live through:
- **Gifts** — Viewers send virtual gifts; creators get a cut
- **Creator Fund** — Monthly payments based on views (if eligible)
- **Brand partnerships** — Sponsored streams with brands

---

## API Approval & Rate Limits

TikTok API approval is **very competitive**. The approval process:

1. **Initial Application Review** (1–2 weeks)
2. **OAuth Application Review** (1–2 weeks)
3. **Livestream Scope Request** (1–4 weeks, may be denied)

**Note**: Many applications are denied at the livestream scope stage. TikTok wants to protect livestream quality and user experience.

### Rate Limits (If Approved)

| Endpoint | Limit |
|---|---|
| **Livestream Info** | 100 requests per hour |
| **Create Livestream** | 10 per day (typical) |
| **User Info** | 1000 requests per hour |

If you hit rate limits, TikTok may require you to upgrade your API tier (paid) or reduce polling frequency.

---

## Why TikTok API Access Is Restricted

TikTok restricts livestream API access for several reasons:

1. **Content Quality**: Livestreams must meet platform standards
2. **Moderation**: TikTok wants direct control over content moderation
3. **Engagement**: TikTok wants streams driven by native TikTok features
4. **Monetization**: TikTok wants to control monetization mechanisms
5. **Account Safety**: TikTok requires strict verification to prevent fraud

---

## Alternatives (Until API Is Available)

Until TikTok opens livestream APIs to independent applications:

1. **Stream manually to TikTok**:
   - Use TikTok's native "Go Live" button in the app
   - Broadcast from your phone or use TikTok Live Studio (desktop)

2. **Use RTMP encoding** (if available):
   - Some streaming software supports TikTok RTMP directly
   - OBS may have experimental TikTok support (check documentation)

3. **Monitor TikTok engagement separately**:
   - Track comments and gifts in TikTok app
   - Use TikTok Analytics for viewership metrics
   - Manually relay important information to your unified system

---

## How to Request TikTok Support

If you want TikTok livestream APIs to be available:

1. **Contact TikTok Creator Support**: Use [TikTok Help Center](https://support.tiktok.com/)
2. **Apply for Developer Program**: Complete application and explain your use case
3. **Request Livestream Scope**: Be specific about why you need programmatic livestream access
4. **Provide detailed use case**: Explain the value for TikTok creators

The more creators request this feature, the more likely TikTok will consider it.

---

## What's Next? (If Approval Comes)

Once TikTok Live API access is approved and integrated into Thiccdal:

1. **Multi-Platform Broadcasting**: Stream to Twitch, YouTube, Facebook, Discord, X, **and TikTok** simultaneously
2. **Creator Economy**: Monetize on TikTok through gifts while managing all platforms from one interface
3. **Unified Analytics**: See viewer counts, engagement, and gifts from all platforms in one dashboard
4. **Cross-Platform Strategy**: Optimize streaming strategy based on which platforms drive the most revenue/engagement
5. **Gift Tracking**: Monitor TikTok gift donations alongside Super Chats (YouTube) and Twitch bits

---

## Status & Timeline

- **Current**: Awaiting API approval (placeholder code in repository)
- **Known Blocker**: TikTok does not publicly offer livestream APIs for independent applications
- **Approval Timeline**: Unknown — could be months or never (TikTok may decide not to open livestream APIs)
- **Approval Risk**: HIGH — many applications are denied at livestream scope stage
- **Target Availability**: Unknown — entirely dependent on TikTok's product decisions

---

## Known Challenges

1. **No Public Livestream API**: Unlike Twitch or YouTube, TikTok does not have a well-documented livestream API
2. **Approval Uncertainty**: TikTok's approval process is opaque and may be denied without detailed feedback
3. **Monetization Complexities**: TikTok tightly controls gift monetization; third-party integration may be complicated
4. **Geographic Variability**: Livestream eligibility varies significantly by region
5. **Mobile-First**: TikTok prioritizes mobile streaming; desktop RTMP support may be limited

---

## Support

For questions or to advocate for TikTok API access:
- **GitHub Issues**: [Request TikTok integration](https://github.com/ThindalTV/Thiccdal26/issues)
- **TikTok Support**: Contact TikTok Creator Support to request livestream API access
- **TikTok Developer Docs**: [TikTok Developer Platform](https://developers.tiktok.com/)
- **Architecture**: See `/docs/architecture/` for technical details

---

**Note**: This document describes a feature that **likely cannot be built without TikTok's explicit approval and partnership**. We are waiting for TikTok to expand their API offerings. Given TikTok's restrictive approach to livestream APIs, approval is uncertain. If this feature is important to you, contact TikTok Support and request livestream API access for third-party applications.
