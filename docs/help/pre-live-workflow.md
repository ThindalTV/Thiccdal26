# Pre-Live Workflow: Preparing Your Stream

This guide walks you through Thiccdal's **Pre-Live mode**, a structured preparation checklist that ensures your stream is ready to go live before you press the **Go Live** button.

---

## Overview

When you start Thiccdal, the dashboard opens in **Pre-Live mode** — a dedicated interface for stream preparation. Pre-Live mode focuses on verifying all technical, platform, and content requirements before streaming begins.

Once all required checks pass, you confirm via the **Go Live** button, which:
- Saves a snapshot of the checklist against a new stream session
- Switches the dashboard to **Live mode** for operational monitoring

Thiccdal doesn't handle your video. Start and stop the broadcast in OBS, which publishes to each
platform directly. **Go Live** marks the session in Thiccdal so chat, the bot, and the overlay are
tracked against it.

---

## The Pre-Live Dashboard Layout

The Pre-Live dashboard is split into two main areas:

```
┌─────────────────────────────────────────────────────┐
│  Top Bar: Stream Status | Platform Badges | Go Live Button │
├──────────────────────┬──────────────────────────────┤
│                      │                              │
│   Left Panel         │     Right Panel              │
│                      │                              │
│  • Stream Info       │  • Pre-Live Checklist        │
│  • Overlay Test      │    (scrollable)              │
│                      │  • Progress bar              │
│                      │  • Item categories           │
└──────────────────────┴──────────────────────────────┘
```

### Top Bar

| Region | Shows | Function |
|---|---|---|
| **Left** | Stream status badge (**Pre-Live**) + platform connection indicators | Quick visual check of platform readiness |
| **Right** | **Go Live** button | Disabled (greyed out with badge showing items remaining) until all required items are checked |

### Left Panel: Stream Info & Overlay Test

**Stream Info Quick-Set:**
- **Title** field — Enter your stream title (e.g., "Building Thiccdal Live")
- **Category** field — Enter the category/game (e.g., "Science & Technology")
- **Tags** field — Comma-separated tags (e.g., "csharp,dotnet,blazor")
- **Update All Platforms** button — Pushes your title, category, and tags to all connected platforms at once

After you enter these values, the button becomes tappable. Press it to verify all platforms accept the stream info. Per-platform results display inline:
- ✅ **OK** — Platform accepted the info
- ❌ **Error** — Platform rejected it (check connection or permissions)

The Pre-Live Checklist automatically checks off **"Stream title set"** and **"Category/game set"** when you fill these fields in.

**Overlay Test Area:**
- A section with buttons for each registered overlay component (Chat Feed, Event Ticker, Lower Third, etc.)
- Pressing **[Test Flash]** for any overlay triggers a test animation so you can verify it displays correctly in OBS
- Use this before checking off the corresponding checklist items

### Right Panel: Pre-Live Checklist

A scrollable checklist organized into **categories**. Each category contains individual items. A progress bar at the top shows `N of M items checked`.

The checklist has four **item types**:

| Type | Behavior |
|---|---|
| **Manual** | You tap to check; no system verification |
| **Auto** | System monitors a condition and checks automatically (you can't uncheck it) |
| **Auto-with-warn** | Auto-checked, but shows a warning if unchecked at Go Live |
| **Action** | Has a button; you must perform the action before checking |

---

## Pre-Live Checklist Categories

### 🔌 Platform Connections *(auto, required)*

All enabled platforms must be in **Connected** state before you can go live.

| Item | Type |
|---|---|
| Twitch chat connected | Auto |

**What if a platform shows "Not Connected"?**
1. Check the **platform badge** in the top bar — it should show green "Connected"
2. If disconnected, click the badge to re-connect
3. Verify your platform credentials are correct in the setup guides (see [Platform Setup Index](./platform-setup-index.md))
4. Restart Thiccdal if the issue persists

### 📋 Stream Info *(required)*

| Item | Type | Notes |
|---|---|---|
| Stream title set | Auto | Checks when the Title field is non-empty |
| Category/game set | Auto | Checks when the Category field is non-empty |
| Platform manual settings reviewed | Action | Opens a summary of platform-specific reminders you should review before going live |

**Platform Manual Settings Action:**
When you tap **[Review Platform Settings]**, a dialog appears listing Twitch's manual reminders — stream encoding, stream delay, extensions, and ad schedule.

Review these reminders, then tap **Confirm** to check off this item. These are informational only — you'll handle them in the Twitch Creator Dashboard before or after going live.

### 🎬 OBS & Technical *(manual, required)*

| Item | Notes |
|---|---|
| OBS scene configured and active | Confirm the correct OBS scene is live before starting the real stream |
| Audio levels checked | |
| Test stream completed | Optional dry-run confirmation |

**How do I configure OBS?**
Point OBS at each platform you're broadcasting to, using that platform's own server URL and stream
key. Thiccdal doesn't sit between OBS and the platforms, so there's no ingest URL to copy.

### 🖥 Overlay Verification *(action, required)*

Each registered overlay component appears here. You'll see items like:

| Item |
|---|
| Chat feed overlay visible |
| Event ticker overlay visible |
| Lower third overlay visible |
| Prompter overlay visible |
| *(additional registered overlay components)* |

**How to verify each overlay:**

1. **For most overlays** (Chat, Event Ticker, Lower Third): Press **[Test Flash]**
   - The overlay will flash/animate in OBS for 2–3 seconds
   - Verify it appears in the correct position and with the correct content
   - If it looks good, tap the checkbox to confirm

2. **For Prompter**: Press **[Open Prompter]**
   - A new window/tab opens to the Teleprompter view
   - Verify the teleprompter page displays correctly and is responsive
   - Close the window, then check off this item in the checklist

### ✔ Personal Prep *(manual, optional)*

| Item | Notes |
|---|---|
| Notifications silenced | Optional — helpful to avoid distractions during stream |
| Water/drinks ready | Optional — stay hydrated! |
| Microphone arm/positioning set | Optional — physical preparation |

**Can I add my own personal prep items?**
Yes! Custom items are stored in the database and appear in this category. Contact your administrator if you want to add site-wide custom items.

---

## Go Live Button States

The **Go Live** button in the top-right shows different states as you complete the checklist:

| State | Appearance | What's Happening |
|---|---|---|
| **Locked** | Greyed out with badge `✗ N items remaining` | Required items are unchecked. Button is not tappable. |
| **Ready** | Glows green with badge `✓ Ready to go live` | All required items checked. Button is tappable. |
| **Confirming** | (dialog appears) | You tapped the button; a confirmation dialog shows a summary. |
| **Streaming** | (switches to Live mode) | Confirmed; switched to Live mode. **Go Live** replaced by **Go Offline** button. |

### Go Live Confirmation Dialog

When you tap **Go Live** and all required items are checked, a dialog appears:

```
═══════════════════════════════════════════
  READY TO GO LIVE?
───────────────────────────────────────────
  Title: Building Thiccdal Live
  Category: Science & Technology
  Platforms: Twitch
  
  ⚠ Optional items not checked:
    • Notifications silenced
    • Water/drinks ready
───────────────────────────────────────────
  [Cancel]  [Go Live Now]
═══════════════════════════════════════════
```

**What this dialog shows:**
- A summary of your **Stream Info** (title, category, platforms)
- Any **optional items** that are not checked (warnings only; these don't block going live)
- Two buttons: **Cancel** (stay in Pre-Live) or **Go Live Now** (confirm and switch to Live mode)

**What happens when you tap "Go Live Now"?**
1. Thiccdal saves the checklist snapshot and opens a stream session
2. Dashboard switches to **Live mode**
3. **Go Live** button replaced by **Go Offline** button
4. Left panel changes to show **Chat Feed** instead of Stream Info
5. Right panel changes to show **Operational Status** instead of Checklist

Start the broadcast in OBS separately — Thiccdal doesn't start or stop it for you.

---

## Transitioning to Live Mode

Once you confirm **Go Live Now**, you enter **Live mode**. The dashboard redesigns for streaming operations:

| Change | Before (Pre-Live) | After (Live) |
|---|---|---|
| **Top bar status** | "Pre-Live" | "Live ●" (with uptime) |
| **Go Live button** | Greyed/glowing (disabled/enabled) | Replaced by **Go Offline** button |
| **Left panel** | Stream Info + Overlay Test | **Chat Feed** (unified, platform-badged) |
| **Right panel** | Pre-Live Checklist | Operational Status + Quick Actions |

In Live mode, the focus shifts from preparation to:
- Monitoring incoming chat from all platforms
- Managing questions and event alerts
- Controlling the teleprompter
- Responding to bot commands
- Watching platform connection health

For details on Live mode operation, see [Live Mode Operations](#) (coming soon).

---

## Quick Troubleshooting

### Q: The Go Live button is still locked even though I've checked everything

**A:** Verify all **required** items are actually checked (not just reviewed):
1. Refresh the page (F5) to re-scan checklist state
2. Check each category:
   - **Platform Connections**: All enabled platforms show green badges
   - **Stream Info**: Title and Category fields are non-empty
   - **OBS & Technical**: All items manually checked
   - **Overlay Verification**: All overlay tests passed and items checked
3. If still locked, check the browser console (F12) for errors and share with support

### Q: A platform disconnected mid-stream

**A:**
1. Your OBS broadcast is unaffected — only Thiccdal's chat and event feed for that platform stops
2. In Live mode, tap the platform badge to re-connect
3. If re-connection fails, check platform permissions and API credentials
4. You can continue streaming and troubleshoot the platform during the stream

### Q: How do I change stream title/category after going live?

**A:**
1. Go Offline first (tap **Go Offline**, confirm)
2. Stream switches back to Pre-Live mode
3. Edit title/category in the Stream Info section
4. Press **Update All Platforms** to push changes
5. When ready, tap **Go Live** again

### Q: Can I use Pre-Live mode without going live?

**A:** Yes! You can:
- Test overlay components without starting the stream
- Verify platform connections
- Configure stream metadata
- Walk through the checklist

Just don't tap **Go Live**. When you're done testing, close or refresh the page.

---

## Tips for Smooth Pre-Live Operations

1. **Arrive early**: Allocate 10–15 minutes before your scheduled stream start for Pre-Live checks
2. **Test overlays first**: Verify your overlay components work before tapping the checklist items
3. **Verify audio levels**: Use OBS's audio meter to confirm mic levels are healthy
4. **Check platform badges**: Green = connected; red = error; yellow = pending
5. **Save your stream info**: Once entered, title/category are stored and reappear on future streams
6. **Have a backup checklist**: Print or screenshot the Pre-Live Checklist as a reference during setup
7. **Use mobile for checklist**: If your OBS monitor is cramped, open Thiccdal on a tablet or phone to see the checklist full-screen

---

## Next Steps

- **Ready to stream?** Follow the [Pre-Live Checklist](#pre-live-checklist-categories) above
- **Need platform help?** See [Platform Setup Index](./platform-setup-index.md)
- **Troubleshooting stream issues?** Check the [Quick Troubleshooting](#quick-troubleshooting) section above

---

**Last Updated**: Phase 10 (Pre-Live Workflow Implementation)

For questions or issues, [open an issue on GitHub](https://github.com/ThindalTV/Thiccdal26/issues).
