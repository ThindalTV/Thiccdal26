# Platform Setup Guide Index

Thiccdal connects to Twitch for chat, events, and stream metadata. It doesn't carry your video —
OBS publishes to Twitch directly.

---

## Available Guides

| Platform | Status | Guide | Requirements |
|---|---|---|---|
| **Twitch** | ✅ Available | [Connecting to Twitch](./connecting-to-twitch.md) | OAuth credentials, Twitch Developer App |

### [Twitch](./connecting-to-twitch.md)

- **Status**: Fully integrated and production-ready
- **Key Features**: OAuth 2.0, EventSub webhooks, Helix API, chat integration, event tracking
- **Requirements**: Twitch Developer App (Client ID + Secret), OAuth redirect URL
- **Typical Setup Time**: 10–15 minutes

---

## Adding Other Platforms

Thiccdal keeps a modular adapter architecture: every integration implements `IPlatformConnection`
and lives in its own project under `src/Remote/`. Twitch is the only adapter shipped today.
`Thiccdal.Remote.Null` is a no-op adapter used as a stand-in during development and testing.

To add a platform, see the `platform-adapter` guidance in `.claude/skills/`.

---

## Getting Started Flow

### 1. Connect Twitch

1. Follow [Connecting to Twitch](./connecting-to-twitch.md) step by step
2. Gather your Client ID, Client Secret, and OAuth redirect URL
3. Configure Thiccdal

### 2. Start Streaming

1. Start your broadcast in OBS, publishing to Twitch with your Twitch stream key
2. Chat appears in Thiccdal's unified feed
3. Use bot commands and overlays during the stream

### 3. Monitor & Optimize

- Track viewer counts and engagement
- Configure filters and bot commands
- Use event tracking to monitor follows, subscriptions, and channel point redeems

---

## Troubleshooting

### I've completed setup but Twitch still shows "Not Connected"

**Check these things:**

1. **Credentials**: Verify your Client ID and Secret are correct and not expired
2. **OAuth Redirect URL**: Ensure it exactly matches both your configuration and the Twitch app settings
3. **Network**: Check that Thiccdal can reach the internet and Twitch's API servers
4. **Permissions**: Verify your account has the required scopes
5. **Logs**: Check Thiccdal's logs for specific error messages

See the troubleshooting section of the Twitch guide for more help.

---

## Frequently Asked Questions

**Q: Can Thiccdal stream to multiple platforms?**

A: No. Thiccdal is a Twitch bot and overlay. It never handles video — OBS publishes to Twitch directly.

**Q: What if I need to re-authenticate?**

A: The Twitch guide includes a "Disconnecting or Re-authenticating" section. Usually you can disconnect, wait 10 seconds, and re-authorize.

---

## Support

For questions or issues:

1. **Read the Twitch Guide**: It has a troubleshooting section
2. **Check GitHub Issues**: Search [existing issues](https://github.com/ThindalTV/Thiccdal26/issues) for your problem
3. **Open a New Issue**: If you can't find help, [create a bug report or feature request](https://github.com/ThindalTV/Thiccdal26/issues/new)
4. **Architecture Documentation**: See `/docs/architecture/` for technical details
