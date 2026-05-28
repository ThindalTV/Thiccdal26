# Connecting Thiccdal to X

This guide explains how to connect Thiccdal to X for **manual post-thread monitoring**.

> **⚠️ Current Status**: Thiccdal can poll replies, likes, and reposts for a configured X post ID, and it can post replies when OAuth 1.0a user tokens are configured. Thiccdal still **cannot create an X Live broadcast, obtain RTMP ingest credentials, or stop a broadcast automatically**.

---

## What works today

- ✅ Poll replies from a configured X post thread
- ✅ Track likes and reposts for that post
- ✅ Post replies to that post when user-context tokens are configured
- ❌ Create or stop X Live broadcasts from Thiccdal
- ❌ Obtain RTMP server URLs or stream keys from Thiccdal
- ❌ Auto-fanout video to X through the current `IStreamTarget` contract

---

## Prerequisites

You need:

- An X developer account
- An app in the X developer portal
- A bearer token for read polling
- OAuth 1.0a user credentials (`ApiKey`, `ApiKeySecret`, `AccessToken`, `AccessTokenSecret`) if you want Thiccdal to post replies
- A manually created X post that will act as the tracked conversation root

---

## Configure Thiccdal

Add this to `appsettings.json`:

```json
{
  "X": {
    "ApiKey": "your-consumer-key",
    "ApiKeySecret": "your-consumer-secret",
    "AccessToken": "your-access-token",
    "AccessTokenSecret": "your-access-token-secret",
    "BearerToken": "your-bearer-token",
    "BroadcastTweetId": "the-post-id-you-created-manually-on-x",
    "PollIntervalMs": 16000,
    "LikesPollIntervalMs": 30000,
    "AuthorizationUrl": "https://developer.x.com/en/portal/dashboard",
    "Channel": "@yourhandle"
  }
}
```

### Option notes

- `BroadcastTweetId`: required for reply, like, and repost monitoring
- `PollIntervalMs`: reply-thread polling interval; default `16000`
- `LikesPollIntervalMs`: like/repost polling interval; default `30000`
- `AuthorizationUrl`: developer-portal URL surfaced in the operator UI
- `Channel`: display label used in normalized events

---

## Operator workflow

### 1. Create the tracked X post manually

Before connecting Thiccdal:

1. Publish the announcement or conversation-root post on X
2. Copy the post ID from its URL
3. Save that value to `X:BroadcastTweetId`

### 2. Start Thiccdal

1. Open the Integrations screen
2. Select X
3. Use the developer portal link if you need to verify or rotate tokens
4. Click **Connect**

### 3. What Thiccdal does after connect

Thiccdal will:

- poll the reply thread using recent search
- map replies into normalized chat events
- poll liking users and reposting users on a slower interval
- back off to the X rate-limit reset time when `x-rate-limit-remaining` reaches zero

---

## Important limitation: no automatic X relay creation

Thiccdal does **not** create X Live broadcasts, does **not** mint RTMP URLs or stream keys, and does **not** stop X broadcasts on your behalf.

If you already have an X-approved manual live workflow, keep using that for the actual video path. Thiccdal's X integration currently covers the **conversation and engagement side only**.

---

## Troubleshooting

### "Connect succeeded but no X events are arriving"

Possible causes:

- `BroadcastTweetId` is blank or wrong
- the configured post has no new replies yet
- your bearer token is missing or invalid
- X rate limiting has delayed the next poll window

### "Replies cannot be sent"

Possible causes:

- `BroadcastTweetId` is blank
- OAuth 1.0a user tokens are missing
- X rejected the write request for your current product tier or app permissions

### "Why can't Thiccdal create the X broadcast for me?"

Because the current public X developer flow used by this integration does not give Thiccdal a real automated RTMP ingest creation path, and the app's current relay contract does not expose start/stop broadcast lifecycle methods for X.
