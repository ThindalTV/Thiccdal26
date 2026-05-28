# Discord Integration Guide

This guide explains how to set up Discord integration with Thiccdal to enable chat aggregation and stream relay to your Discord server.

## Overview

The Discord integration allows Thiccdal to:
- Read chat messages from a designated Discord text channel
- Send bot messages to the Discord channel
- React to user events (joins, leaves, reactions)
- Surface an explicit blocked state for Discord voice-channel relay so operators do not mistake chat connectivity for supported video relay

## Prerequisites

- A Discord account
- Administrator access to the Discord server where you want to add Thiccdal
- Basic understanding of Discord's Developer Portal

## Step 1: Create a Discord Bot Application

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
2. Click **New Application**
3. Give your application a name (e.g., "Thiccdal Bot")
4. Click **Create**

## Step 2: Configure Your Bot

1. In the left sidebar, click **Bot**
2. Click **Add Bot** (if not already added)
3. **Important:** Under "Privileged Gateway Intents," enable:
   - **SERVER MEMBERS INTENT** (to track user joins/leaves)
   - **MESSAGE CONTENT INTENT** (required to read message content)
   
   *Without MESSAGE CONTENT INTENT, the bot will not be able to read chat messages.*

4. Under the **Token** section, click **Reset Token** and copy the bot token
   - **Keep this token secret!** Anyone with this token can control your bot.
   - Save it somewhere secure; you'll need it for configuration later.

## Step 3: Invite the Bot to Your Server

1. In the left sidebar, click **OAuth2** → **URL Generator**
2. Under **SCOPES**, select:
   - `bot`
3. Under **BOT PERMISSIONS**, select:
   - **View Channels**
   - **Send Messages**
   - **Read Message History**
   - **Add Reactions** (optional, but recommended)
   - **Connect** (optional; only needed if you want to preconfigure a future Discord voice target)
   - **Speak** (optional; only needed if you want to preconfigure a future Discord voice target)
4. Copy the generated URL at the bottom of the page
5. Open the URL in a new browser tab
6. Select the Discord server where you want to add the bot
7. Click **Authorize**

## Step 4: Get Your Server and Channel IDs

Discord uses numeric "snowflake" IDs to identify servers and channels.

### Enable Developer Mode

1. Open Discord
2. Click the **Settings** gear icon (bottom-left)
3. Go to **App Settings** → **Advanced**
4. Enable **Developer Mode**

### Get Your Guild (Server) ID

1. Right-click on your server name in the server list
2. Click **Copy Server ID**
3. Save this ID — you'll use it as `GuildId` in configuration

### Get Your Stream Text Channel ID

1. Right-click on the text channel you want Thiccdal to read/send chat messages
2. Click **Copy Channel ID**
3. Save this ID — you'll use it as `StreamChannelId` in configuration

### (Optional) Get Your Voice Channel ID

If you want Thiccdal to track the intended Discord voice/stage destination:

1. Right-click on the voice or stage channel you want to relay your stream
2. Click **Copy Channel ID**
3. Save this ID — you'll use it as `VoiceChannelId` in configuration
4. Today this remains operator metadata only. Thiccdal will log that Discord relay is blocked instead of pretending the stream can be published.

## Step 5: Configure Thiccdal

Edit your `appsettings.json` or user secrets to include the Discord configuration:

```json
{
  "Discord": {
    "BotToken": "YOUR_BOT_TOKEN_HERE",
    "GuildId": "YOUR_GUILD_ID_HERE",
    "StreamChannelId": "YOUR_STREAM_CHANNEL_ID_HERE",
    "VoiceChannelId": "YOUR_VOICE_CHANNEL_ID_HERE_OPTIONAL",
    "ReconnectDelaySeconds": 5
  }
}
```

### Configuration Options

| Option | Required | Description |
|--------|----------|-------------|
| `BotToken` | ✅ Yes | Your Discord bot token from the Developer Portal |
| `GuildId` | ✅ Yes | The snowflake ID of your Discord server |
| `StreamChannelId` | ✅ Yes | The snowflake ID of the text channel for stream chat |
| `VoiceChannelId` | ❌ No | The snowflake ID of the intended voice/stage channel. Thiccdal records it, but Discord bot video relay is currently blocked by API/library limitations. |
| `ReconnectDelaySeconds` | ❌ No | Delay in seconds before reconnecting after a disconnect (default: 5) |

## Step 6: Start Thiccdal

1. Start the Thiccdal application
2. The bot should automatically connect to Discord and appear online in your server
3. Check the logs for confirmation:
   ```
   [Discord] Discord bot connected successfully
   [Discord] Discord connection state: Connected
   ```

## Troubleshooting

### Bot is not connecting

- **Check your bot token:** Ensure the `BotToken` in configuration matches the token in the Discord Developer Portal
- **Verify privileged intents:** Make sure **MESSAGE CONTENT INTENT** is enabled
- **Check IDs:** Ensure `GuildId` and `StreamChannelId` are correct snowflake IDs (numeric)
- **Review logs:** Look for error messages in the Thiccdal console output

### Bot is online but not reading messages

- **Privileged intents:** Ensure **MESSAGE CONTENT INTENT** is enabled in the Developer Portal
- **Channel permissions:** Verify the bot has **View Channels** and **Read Message History** permissions in the target channel
- **Correct channel:** Double-check that `StreamChannelId` matches the channel you're testing in

### Bot cannot send messages

- **Channel permissions:** Ensure the bot has **Send Messages** permission in the target channel
- **Configuration:** Verify `StreamChannelId` is correct

### Bot disconnects frequently

- **Network issues:** Check your internet connection
- **Token validity:** Verify the bot token has not been reset or revoked
- **Rate limiting:** Discord may temporarily block the bot if it's sending too many requests

### Voice relay shows as blocked

- **Expected today:** Discord bot chat/event integration is supported, but Discord Go Live video relay is not
- **Why:** Discord's public bot API and Discord.Net voice stack cover audio voice connectivity, not a production-safe bot video publish path from RTMP ingest
- **What Thiccdal does:** keeps chat online, logs the blocked relay reason, and refuses to report relay startup success

## Security Best Practices

1. **Never share your bot token** — treat it like a password
2. **Use environment variables or user secrets** for production deployments instead of hardcoding tokens in `appsettings.json`
3. **Regenerate your token immediately** if you suspect it has been compromised
4. **Grant minimal permissions** — only enable the bot permissions you actually need

## Next Steps

- Configure other platform integrations (Twitch, YouTube, etc.)
- Set up the chatbot to respond to Discord messages
- Configure the overlay to display Discord chat on your stream

## Support

If you encounter issues not covered in this guide, please:
- Check the [Thiccdal documentation](../README.md)
- Review the logs for detailed error messages
- Open an issue on the GitHub repository with log excerpts and configuration (redact sensitive tokens)

---

**Note:** Discord `VoiceChannelId` is intentionally non-operative for RTMP relay right now. Thiccdal keeps the configuration visible, but the adapter blocks relay startup until Discord exposes a supported bot video transport and Thiccdal has a matching streaming path.
