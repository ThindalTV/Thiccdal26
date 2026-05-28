# Thiccdal – Architecture Overview

> **Status:** Living document. Updated as decisions are made and features are built.
> See `/architecture/` for individual Architectural Decision Records (ADRs).

---

## 1. Purpose

Thiccdal is a streaming command-and-control system. It runs on a stream PC and is operated
from a separate device (e.g., a Surface Pro tablet via browser). A single operator interface
gives full visibility and control of all platform connections, chat, events, overlays, and
stream output — without needing to switch between apps or screens.

---

## 2. High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         Stream PC (Server)                               │
│                                                                          │
│  OBS Studio                                                              │
│      │ RTMP push                                                         │
│      ▼                                                                   │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  Thiccdal.Streaming (RTMP Ingest + Fanout)                       │   │
│  │   ├── Relay → Twitch RTMP                                        │   │
│  │   ├── Relay → YouTube RTMP                                       │   │
│  │   ├── Relay → Discord RTMP                                       │   │
│  │   └── Record to disk                                             │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  Thiccdal (Blazor Server host)                                   │   │
│  │   ├── Thiccdal.Modules.Control      (operator UI, touch-friendly)│   │
│  │   ├── Thiccdal.Modules.Teleprompter (combined chat + events)     │   │
│  │   ├── Thiccdal.Modules.Overlay      (SignalR → OBS browser src)  │   │
│  │   └── Status Endpoint               (online/offline image/enum)  │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  Thiccdal.Infrastructure                                         │   │
│  │   └── Interfaces  (IPlatformConnection, IChatService, …)         │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  Thiccdal.Data                                                   │   │
│  │   ├── ApplicationDbContext  (SQLite / EF Core)                   │   │
│  │   ├── Entity Models         (User, ChatMessage, PlatformEvent…)  │   │
│  │   └── Migrations                                                 │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Remote Platform Adapters                                         │  │
│  │   ├── Thiccdal.Remote.Twitch                                      │  │
│  │   ├── Thiccdal.Remote.YouTube                                     │  │
│  │   ├── Thiccdal.Remote.Facebook                                    │  │
│  │   ├── Thiccdal.Remote.X                                           │  │
│  │   ├── Thiccdal.Remote.Discord                                     │  │
│  │   ├── Thiccdal.Remote.LinkedIn  ⚠ UI disabled until API approved │  │
│  │   ├── Thiccdal.Remote.TikTok   ⚠ UI disabled until API approved  │  │
│  │   └── Thiccdal.Remote.Null    (logging-only, used in tests)       │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
                          ▲                  ▲
             Blazor Server │                 │  Overlay browser source
             circuit (WS)  │                 │  (OBS browser plugin, WS)
                    ┌──────┴───────┐   ┌─────┴────────────┐
                    │ Control UI   │   │  Overlay Page     │
                    │ (any browser)│   │  (OBS/browser)    │
                    │ multi-client │   │                   │
                    └──────────────┘   └──────────────────┘
```

### Project & Module Layout

```
/src/Thiccdal/                          Blazor Server host
/src/Thiccdal.Infrastructure/           Interfaces, enums, value types — no EF Core
  Bot/                                    IChatService, chat event models
    Models/                               ChatEvent, PlatformEvent, RawEvent, PlatformEventSource
  Remotes/                                IChatSource (per-platform chat adapter contract)
  Teleprompter/                           ITeleprompterService, ScrollDirection, ScrollEventArgs
  Twitch/                                 ITwitchService, ITwitchTokenManager, TwitchOptions
/src/Thiccdal.Data/                     EF Core DbContext, entities, migrations
  Models/                                 Entity classes (e.g. TwitchToken)
  Migrations/                             EF Core migration files
/src/Thiccdal.API/                      HTTP status and control endpoints
/src/Thiccdal.Streaming/               RTMP ingest, fanout, recording
/src/Modules/
  Thiccdal.Modules.ChatBot/             Chat aggregation + command dispatch (Razor Class Library)
    Services/
  Thiccdal.Modules.Control/             Command & Control operator UI (Razor Class Library)
    Components/
    Layout/
    Pages/
    Services/
  Thiccdal.Modules.Overlay/             OBS browser-source overlay (Razor Class Library)
    Components/
    Pages/
    Services/
  Thiccdal.Modules.Teleprompter/        Teleprompter display (Razor Class Library)
    Components/
    Models/
    Pages/
    Services/
/src/Shared/
  Thiccdal.Shared.Components/           Shared primitive UI components (Razor Class Library)
    Components/
      Primitives/                          InputContainer, TextBox, NumberBox, CheckBox, …
    Models/                                SelectOption and other shared data types
/src/Remote/
  Thiccdal.Remote.Twitch/
  Thiccdal.Remote.YouTube/
  Thiccdal.Remote.Facebook/
  Thiccdal.Remote.X/
  Thiccdal.Remote.Discord/
  Thiccdal.Remote.LinkedIn/              ⚠ disabled until API approved
  Thiccdal.Remote.TikTok/               ⚠ disabled until API approved
  Thiccdal.Remote.Null/                  logging-only; used in tests
/src/Aspire/
  Thiccdal.Aspire.AppHost/              Aspire AppHost
  Thiccdal.Aspire.ServiceDefaults/      Aspire ServiceDefaults
/src/Tests/
  Thiccdal.Tests/                        Main project tests
  Thiccdal.Data.Tests/                   Data layer tests
  Remote/
    Thiccdal.Remote.Twitch.Tests/
/docs/architecture/                     Architecture .md files
```

---

## 3. Feature Descriptions

### 3.1 RTMP Multicast (Thiccdal.Streaming)

OBS pushes a single RTMP stream to Thiccdal. The streaming subsystem ingests it and fans it
out to multiple platform RTMP endpoints concurrently. Each relay target is independently
configured (URL, stream key) and independently monitored.

- If the ingest stream disconnects, a "Be Right Back" slate is injected into all relay outputs
  so viewers see a placeholder rather than a frozen or dropped stream.
- All active relay sessions are recorded to disk. Recording metadata (start/end time, file path,
  platform, error state) is persisted in the database.
- Restream runtime control plus ingest and recording configuration are exposed through
  Thiccdal-owned API endpoints so the operator configuration view can manage them without editing
  JSON files by hand.
- The current in-repo implementation uses LiveStreamingServerNet for ingest and FFmpeg for
  per-destination relay and BRB processes. Only adapters that expose concrete RTMP destinations
  participate in live fanout today.
- Each relay target is an `IStreamTarget` implementation. Adding a new platform requires
  creating a new project under `/src/Remote/` and registering it.

### 3.2 Platform Connection Abstraction

Every platform adapter (Twitch, YouTube, Facebook, X, Discord, LinkedIn, TikTok, Null)
implements `IPlatformConnection`, which combines three concerns:

| Sub-interface | Responsibility |
|---|---|
| `IChatService` | Send and receive chat messages |
| `IStreamTarget` | Accept and relay RTMP stream data |
| `PlatformEventSource` | Emit typed `PlatformEvent` objects |

The `Null` implementation logs every operation at `Information` level and emits no traffic.
It is the default in unit tests, is suitable for offline development, and is used for full-stack
integration coverage of the host.

LinkedIn is implemented as a full `IPlatformConnection` but its UI entry is rendered as
disabled with a tooltip explaining that LinkedIn Live API access requires platform approval.

### 3.3 Event System

All platform happenings

All platform happenings — subscribes, follows, redeems, raids, likes, superchats — derive
from `PlatformEvent`. Known event types have dedicated record types with additional properties.
Unknown events create a `RawEvent` of the base `PlatformEvent` called RawEvent with a `RawData` field containing the raw
platform payload so that no information is silently discarded.

**Rule:** Every event is persisted to the database *before* it is dispatched to subscribers.
This guarantees a full audit trail regardless of downstream handler failures.

### 3.4 Chat Aggregation & User History

All incoming chat messages, regardless of platform, are normalised to a `ChatMessage` record
and persisted. Each message is linked to a `PlatformUser` record scoped to its platform.
Cross-platform user identity matching (recognising the same person on Twitch and YouTube) is
a stretch goal and will be scaffolded but not initially implemented.

### 3.5 Chatbot

The chatbot listens to all chat sources and dispatches on command triggers (`!<name>`) as well
as firing proactive messages on a configurable timer.

#### Commands
Commands are stored in the database, not in `appsettings.json`. Each `BotCommand` row holds:

| Column | Purpose |
|---|---|
| `Trigger` | The `!<name>` string that activates the command |
| `Response` | Template string sent to chat on match |
| `HandlerType` | Optional: fully-qualified name of a code-side `ICommandHandler` implementation |
| `IsEnabled` | Per-command kill switch |

Commands are managed through a **modal dialog** in the operator UI (not inline on the live
screen) because they are edited infrequently and changes should not be made accidentally
during a stream.

#### Metadata tokens
Response templates support interpolation tokens:

| Token | Resolves to |
|---|---|
| `{user}` | Display name of the requesting chatter |
| `{platform}` | Platform name (Twitch, YouTube, etc.) |
| `{count}` | How many times this command has been triggered this session |
| `{uptime}` | Current stream uptime |

#### Code handlers
If `HandlerType` is set, the dispatcher resolves `ICommandHandler` from DI and calls it
instead of (or in addition to) sending the static response. Handlers are registered at
startup. The static response acts as a fallback if the handler throws.

#### Proactive messaging
A timer-based `IHostedService` sends configured messages on a per-platform interval. Proactive
messages are also stored in the database with a `ProactiveMessage` flag.

Viewer question detection is currently routed through `Thiccdal.AI`, which exposes repository-owned
abstractions over an OpenAI-compatible chat client. The default local target is LM Studio, but the
same boundary can be pointed at any compatible endpoint later without changing chatbot services.

Future: richer AI handlers (for example free-form bot responses) can build on the same abstraction
layer as an opt-in `ICommandHandler`.

### 3.6 Overlay (Thiccdal.Modules.Overlay)

The overlay is a separate Blazor page (`/overlay`) intended to be loaded in OBS as a browser
source. It lives in the `Thiccdal.Modules.Overlay` Razor Class Library and is hosted by the
main Blazor Server app. It receives real-time updates via the Blazor SignalR circuit (or a
dedicated SignalR hub for non-Blazor scenarios). Components are pluggable — each implements
`IOverlayComponent` and is registered in the overlay pipeline.

Initial components:
- Combined chat feed with per-platform source badge
- Event ticker (subscribes, follows, redeems)
- Lower-third for featured questions

#### Testable overlay components
Overlay components that support pre-live verification implement `ITestableOverlayComponent`,
which extends `IOverlayComponent` with a single method: the test flash.

When triggered from the Pre-Live Checklist (see §3.18), the component displays a prominent
full-component overlay for 3 seconds showing **“■ TESTING — [Component Name]”** in large
bold text with a lime/green border pulse. This is deliberately more visible and persistent
than the event flash (see §3.7), because its purpose is confirmation that the overlay is
correctly positioned and visible in OBS, not just a brief notification.

The test is triggered via the same `IOperatorStateService` state mechanism so it fires on all
connected sessions simultaneously (useful when one operator is watching OBS while another
operates the control device).

### 3.7 Teleprompter (Thiccdal.Modules.Teleprompter)

A full-screen page (`/prompter`) showing the combined event and chat feed in large, readable
text. Lives in the `Thiccdal.Modules.Teleprompter` Razor Class Library and is hosted by the
main Blazor Server app. **The teleprompter itself has no interactive controls** — it is a
passive display intended to be shown on a second monitor or screen facing the streamer.

Scrolling is driven entirely from the **Command & Control UI** via shared state. The operator
taps up/down scroll buttons on their control device (Surface Pro) and the prompter view reacts
in real time through the same multi-operator state sync mechanism (see §3.13).

#### Prompter attention flash
Because the prompter is the streamer’s primary on-screen reference, **the prompter page itself
flashes** when something requires attention. Two triggers:

| Trigger | Flash style |
|---|---|
| New question added to the queue | Gradient sweep from the right edge, ~0.5 s, cyan/teal accent |
| Significant platform event (sub, raid, cheer, membership) | Gradient sweep from the right edge, ~0.5 s, gold accent |

The flash is implemented as a CSS animation (`@keyframes`) applied to a fixed-position overlay
div injected by JS interop. It auto-dismisses and does not obstruct the prompter text.

The control UI shows its own separate flash indicator for new questions (in the question queue
panel header — see §3.8), independent of the prompter flash.

### 3.8 Question Queue

Questions posted in any chat are flagged (manually or by a bot command). The queue is displayed
in the operator UI. The operator can:

1. **Dismiss** — remove from queue silently, without showing to viewers.
2. **Feature** — push to the lower-third overlay for viewers to see.
3. **Complete** — marks the question as handled: removes it from the overlay lower-third
   *and* removes it from the queue. Communicates clearly that the question has been addressed,
   not discarded.

All three state transitions are synced across all connected operator sessions.

### 3.9 Command & Control UI (Thiccdal.Modules.Control)

The operator UI lives in the `Thiccdal.Modules.Control` Razor Class Library and operates in
two distinct **modes** that share the same page URL but render different primary content:

#### Pre-Live mode (initial state on startup)

Focused on preparation. The right half of the screen shows the Pre-Live Checklist (see §3.18).
The left half shows a stream-info quick-set panel (title, category, tags) and an overlay
test area.

| Region | Content |
|---|---|
| Top bar (left) | Stream status (**Pre-Live**), per-platform connection status badges |
| Top bar (right) | **Go Live** button — disabled with a badge showing “✗ N items remaining”; glows green and becomes tappable when the checklist is complete |
| Left panel | Stream info quick-set (title, category, tags) + overlay test buttons per component |
| Right panel | Pre-Live Checklist (scrollable, categorised, progress bar at top) |
| Bottom bar | Platform connect/disconnect quick actions |

#### Live mode (after Go Live is confirmed)

Focused on operation. Identical to the original single-screen design.

| Region | Content |
|---|---|
| Top bar (left) | Stream status (**Live ●**, uptime), per-platform connection status badges |
| Top bar (right) | **Go Offline** button (requires confirmation); checklist icon showing “✓ all clear” or “✗ N” for any auto-check that regressed |
| Left panel | Combined chat feed (scrollable, platform badge per message) |
| Centre panel | Teleprompter scroll controls (▲ / ▼ buttons), question queue below |
| Bottom bar | Chatbot controls, timer status, “Manage Commands” button, “Stream Info” button |

Switching from Pre-Live to Live requires pressing **Go Live** and confirming the dialog.
The confirmation dialog shows a final summary of the checklist (any unchecked optional items
are listed as warnings, not blockers).

The **Go Live** action starts the RTMP relay to all enabled platforms simultaneously. It
replaces the previous “start/stop stream relay” quick action.

Responsive breakpoints allow a phone to show a minimal subset (status + question queue in
live mode; status + checklist progress in pre-live mode).

### 3.10 Online/Offline Status Endpoint

A lightweight endpoint (`/status`) returns the current stream state and live session
information, making it useful for external embeds, status pages, and stream overlays.

**`GET /status`** returns a JSON object:

```json
{
  "state": "Online",
  "stream": {
    "title": "Building Thiccdal Live!",
    "category": "Science & Technology",
    "tags": ["csharp", "dotnet", "blazor"],
    "startedAt": "2024-06-01T14:00:00Z",
    "uptime": "01:23:45"
  },
  "platforms": [
    { "name": "Twitch",   "state": "Connected" },
    { "name": "YouTube",  "state": "Connected" },
    { "name": "Facebook", "state": "Connected" },
    { "name": "X",        "state": "Error", "error": "Auth token expired" }
  ]
}
```

When offline, `"stream"` is `null` and all platform states are `"Disconnected"`.

**`GET /status/badge.svg`** returns a static image asset that flips between an online and
an offline graphic. Intended for embedding in GitHub READMEs, websites, or stream panels
without needing a downstream JSON consumer.

This lets external sites embed a live status badge without additional infrastructure.

### 3.11 Stream Recording

When the RTMP ingest is active, Thiccdal records all stream output to disk. Filename includes
date/time and stream session ID. Start time, end time, file path, and any error state are
written to the `StreamRecording` table. If a recording fails to finalise correctly, the
database row is marked with an error and the partial file path is preserved.

### 3.12 LinkedIn Integration

LinkedIn Live requires explicit API access approval from LinkedIn. The full adapter is built
and registered in DI — `IStreamTarget`, `IChatService`, and `IEventSource` are all
implemented — but the platform is marked as **disabled** in the operator UI until approval
is granted.

Behaviour when disabled:
- The LinkedIn connection status badge is rendered with a "pending approval" state.
- Start/connect buttons are disabled with a tooltip explaining the situation.
- The RTMP relay target skips LinkedIn silently (logged at `Debug` level).
- No credentials are required in `appsettings.json` until the integration is activated.

When LinkedIn API access is eventually granted, enabling the integration is a configuration
change only — no code changes required.

### 3.13 Multi-Operator Support

Multiple operator sessions can connect to the same Thiccdal instance simultaneously from day
one — no authentication is required in the initial release.

All operator-visible state (question queue contents, question states, lower-third visibility,
teleprompter scroll position, connection states) is held in singleton state services. State
changes publish to all connected circuits via a `StateChanged` event. Each Blazor component
subscribes in `OnInitializedAsync` and calls `InvokeAsync(StateHasChanged)` on notification.

This means:
- A second operator opening the control UI sees the current live state immediately.
- Any action taken by one operator (completing a question, scrolling the prompter) is
  reflected on all other operator screens within a single render cycle.
- The overlay (`/prompter`, `/overlay`) also receives the same state and reacts identically.

Authentication and role-based access are explicitly out of scope for v1.

### 3.14 Platform Manual Settings Reminders

Some platform settings cannot be controlled via any API and must be configured manually in
each platform’s web dashboard before going live. These are surfaced in the Stream Info dialog
as a per-platform checklist so the operator doesn’t miss them.

The reminders are defined in code as a `IReadOnlyList<PlatformManualReminder>` and are
never stored in the database — they change only when platform capabilities change.

| Platform | Setting | Reminder text |
|---|---|---|
| Twitch | Stream encoding | "Set bitrate, resolution & keyframe interval in OBS" |
| Twitch | Stream delay | "Enable/configure stream delay in Creator Dashboard if needed" |
| Twitch | Extensions | "Activate/configure extensions in Creator Dashboard" |
| Twitch | Ad schedule | "Configure ad schedule in Creator Dashboard" |
| YouTube | Made for Kids | "Confirm ‘Made for Kids’ setting in YouTube Studio" |
| YouTube | Super Chat | "Verify Super Chat & Super Thanks are enabled in YouTube Studio" |
| YouTube | Visibility | "Set visibility to Public in YouTube Studio when ready" |
| YouTube | Age restriction | "Review age restriction setting in YouTube Studio" |
| Discord | Stream permissions | "Configure who can view the stream in server/channel settings" |
| Discord | NSFW | "Review NSFW channel flag in server settings" |
| LinkedIn | All settings | "LinkedIn Live settings must be configured in LinkedIn Studio" |
| Facebook | Privacy | "Set broadcast privacy (Timeline / Page / Group) before going live — cannot change mid-stream" |
| Facebook | App Review | "Confirm Live Video permissions have passed App Review" |
| X | Broadcast Tweet | "Compose the broadcast Tweet text before starting — cannot edit after stream begins" |
| X | API tier | "Verify X API write access tier is active (Basic or higher required)" |
| TikTok | All settings | "TikTok Live settings must be configured in TikTok Studio — API access pending approval" |

New reminders are added by extending the `PlatformManualReminder` list in
`Thiccdal.Infrastructure`; no database migration is required.

### 3.15 Facebook Live Integration

Facebook Live is a high-priority addition. Meta’s Graph API provides full programmatic
control over live videos: create a `LiveVideo` object to obtain an RTMP ingest URL, update
the broadcast title, start/stop the stream, and retrieve viewer counts.

- Chat is read via the Graph API `/{live-video-id}/comments` edge (polling on interval,
  as there is no push mechanism for public live comments).
- Events such as reactions and new followers are available via the Graph API.
- Stream info (title, description, status) is fully settable via API.
- The manual settings reminder covers privacy setting (Timeline vs. Page vs. Group) since
  that must be chosen at `LiveVideo` creation time and cannot be changed mid-stream.

**API access:** A Facebook App with `live_video` and `pages_manage_posts` permissions is
required. These require App Review for production use.

### 3.16 X (Twitter) Live Integration

X Live uses RTMP via the `media/upload` and `statuses/update` endpoints to create a
live broadcast. The broadcast produces a persistent Tweet that viewers can find.

- Chat is read by polling the Tweet’s reply thread via the X API v2 `search/recent`.
- Events (likes, reposts, new follows during a live) are available via filtered stream.
- Title/description maps to the broadcast Tweet’s text (editable before going live only).
- **API tier note:** X API Basic tier is sufficient for read access; write access (posting
  the broadcast Tweet) requires Basic or higher. Verify current tier requirements at
  implementation time — X’s pricing and tier structure changes frequently.

### 3.17 TikTok Live Integration

TikTok Live supports RTMP via TikTok Live Studio. TikTok’s Open API for live streaming
requires explicit approval from TikTok (similar to the LinkedIn Live approval process).

The full adapter is built but the platform is marked as **disabled** in the operator UI
until approval is granted. The same disabled-state pattern as LinkedIn applies (see §3.12).

- Chat read via `live.comment_list` API endpoint.
- Events (gifts, likes, new followers) via `live.event_list`.
- Stream info (title) settable via `live.update` API.

### 3.18 Pre-Live Checklist

Before the stream goes live, the operator works through a structured checklist. The **Go Live**
button is disabled until all *required* items are checked. Optional items produce a warning
summary in the Go Live confirmation dialog but do not block starting.

#### Item types

| Type | Behaviour |
|---|---|
| **Manual** | Operator taps to check; no system verification |
| **Auto** | System monitors a condition and checks automatically (e.g. platform connected) |
| **Auto-with-warn** | Auto-checked but also shows a warning if unchecked at Go Live |
| **Action** | Has a trigger button; the action must be performed before the item can be checked |

#### Checklist categories and items

**🔌 Platform Connections** *(auto, required)*
All enabled platforms must be in `Connected` state. The system monitors `IPlatformConnection.State`
and checks/unchecks items automatically. Disabled platforms (LinkedIn, TikTok) are hidden.

| Item | Type |
|---|---|
| Twitch chat connected | Auto |
| YouTube chat connected | Auto |
| Facebook chat connected | Auto |
| X chat connected | Auto |
| Discord connected | Auto |

**📋 Stream Info** *(required)*

| Item | Type | Notes |
|---|---|---|
| Stream title set | Auto | Checks if `StreamSession.Title` is non-empty |
| Category/game set | Auto | Checks if `StreamSession.Category` is non-empty |
| Platform manual settings reviewed | Action | Opens the Stream Info dialog; operator confirms each platform's manual reminder checklist |

**🎬 OBS & Technical** *(manual, required)*

| Item | Type | Notes |
|---|---|---|
| OBS scene configured and active | Manual | Operator confirms the active OBS scene before going live |
| RTMP ingest URL configured in OBS | Manual | Displays `StreamingOptions.IngestUrl` inline as a copyable reference and auto-checks when copied |
| Audio levels checked | Manual | |
| Test stream completed | Manual | Optional confidence check before the real show starts |

**🖥 Overlay Verification** *(action, required)*

Each registered `ITestableOverlayComponent` appears here. The operator presses **[Test Flash]**,
confirms the overlay is visible and correctly positioned in OBS, then checks the item.

| Item | Type | Notes |
|---|---|---|
| Chat feed overlay visible | Action | [Test Flash] triggers `ITestableOverlayComponent.Test()` |
| Event ticker overlay visible | Action | Same |
| Lower third overlay visible | Action | Same |
| Prompter overlay visible | Action | Navigates to `/prompter` on test; operator verifies |
| *(additional registered overlay components)* | Action | Automatically added as new components register |

**💾 Recording** *(auto-with-warn, required)*

| Item | Type | Notes |
|---|---|---|
| Recording output path configured | Auto | Checks `StreamingOptions` recording path is non-empty |
| Disk space available (≥ 10 GB free) | Auto | Polled from file system at checklist open |

**✔ Personal Prep** *(manual, optional)*

| Item | Notes |
|---|---|
| Notifications silenced | Optional |
| Water/drinks ready | Optional |
| Microphone arm/positioning set | Optional |

> Custom personal prep items can be added to/removed from the database (DB-configurable,
> optional category only). Required and auto items are always code-defined.

#### Go Live button states

| State | Condition | Appearance |
|---|---|---|
| Locked | Any required item unchecked | Greyed out; badge shows `✗ N items remaining` |
| Ready | All required items checked | Glows green; badge shows `✓ Ready to go live` |
| Confirming | Tapped while Ready | Dialog shows summary including any unchecked optional items |
| Streaming | After confirmation | Replaced by **Go Offline** button in Live mode |

#### Checklist state and persistence

The checklist state is held in `IOperatorStateService` so it is synced across all operator
sessions in real time. Each stream session's final checklist state (which items were checked,
timestamps) is optionally persisted to the `ChecklistSession` DB entity for post-stream review.

Custom personal prep items are stored in the `CustomChecklistItem` DB entity.

---

## 4. Data Model (Key Entities)

> Entity classes, `ApplicationDbContext`, and EF Core migrations all live in **`Thiccdal.Data`**.
> Interfaces and shared value types live in **`Thiccdal.Infrastructure`**.
> Platform adapters and the Blazor host reference `Thiccdal.Infrastructure`; only
> `Thiccdal.Data` references `Thiccdal.Infrastructure` for its entity implementations.

```
PlatformUser          — id, platform (enum), platform_user_id, display_name, created_at
ChatMessage           — id, platform_user_id, platform, content, sent_at, raw_data
PlatformEvent         — id, platform, event_type (discriminator), occurred_at, raw_data
  └─ SubscribeEvent   — tier, is_gift, gifter_platform_user_id
  └─ FollowEvent      — (no extra fields beyond base)
  └─ RedeemEvent      — reward_id, reward_title, user_input
  └─ RaidEvent        — raiding_channel, viewer_count
BotCommand            — id, trigger, response_template, handler_type (nullable), is_enabled, use_count
ProactiveMessage      — id, message, interval_seconds, is_enabled, last_sent_at
StreamRecording       — id, session_id, platform, file_path, started_at, ended_at, error
StreamSession         — id, started_at, ended_at, title, category, tags (CSV)
ChecklistSession      — id, stream_session_id, item_id, checked_at (nullable), was_auto_checked
CustomChecklistItem   — id, label, sort_order, is_enabled
```

**Code-side value types** (not DB entities, defined in `Thiccdal.Infrastructure`):
- `PlatformManualReminder` — hardcoded per-platform manual setting reminders (§3.14)
- `ChecklistItemDefinition` — defines each checklist item (id, category, label, type, is_required)
- `ChecklistItemState` — runtime state per item (checked, auto-checked, blocked); held in `IOperatorStateService`

---

## 5. Configuration Shape (IOptions)

```csharp
// appsettings.json → "Streaming"
StreamingOptions
  ├── IngestUrl          string   // e.g. "rtmp://localhost:1935/live"
  └── Targets[]
        ├── Platform     string
        ├── RtmpUrl      string
        └── StreamKey    string   // from env var via binding

// appsettings.json → "Twitch"
TwitchOptions
  ├── DefaultTargetChannel       string
  ├── DefaultBroadcasterId       string
  ├── BotUsername                string
  ├── BotUserId                  string
  ├── ClientId                   string
  ├── ClientSecret               string   // env var
  ├── RedirectUri                string
  ├── OAuthBaseAddress           string
  ├── Helix:BaseAddress          string
  ├── Helix:StreamStateRefreshSeconds int
  ├── EventSub:WebSocketUrl      string
  └── EventSub:ReconnectDelaySeconds int

// appsettings.json → "YouTube"
YouTubeOptions
  ├── ChannelId          string
  └── ApiKey             string   // env var

// appsettings.json → "Discord"
DiscordOptions
  ├── GuildId            string
  ├── StreamChannelId    string
  └── BotToken           string   // env var

// appsettings.json → "Facebook"
FacebookOptions
  ├── PageId             string
  ├── AppId              string
  └── AppSecret          string   // env var

// appsettings.json → "X"
XOptions
  ├── ApiKey             string
  ├── ApiKeySecret       string   // env var
  ├── AccessToken        string   // env var
  └── AccessTokenSecret  string   // env var

// appsettings.json → "TikTok"
TikTokOptions
  ├── IsEnabled          bool     // false until API approved
  └── AccessToken        string   // env var

// appsettings.json → "Chatbot"
ChatbotOptions
  └── ProactiveIntervalSeconds   int   // default interval; per-command overrides in DB
```

---

## 6. UI & Styling Conventions

All UI work across `Thiccdal.Modules.Control`, `Thiccdal.Modules.Overlay`, and
`Thiccdal.Modules.Teleprompter` follows these rules:

### No third-party CSS libraries

The project uses **no third-party CSS frameworks or UI component libraries** (no Bootstrap,
Tailwind, MudBlazor, etc.). All styling is hand-authored.

### Isolated component CSS

Every Razor component places its CSS in a co-located **`.razor.css`** file
(e.g. `ChatFeed.razor` → `ChatFeed.razor.css`). Styles that cannot be isolated (true global
resets, font-face declarations, root variable definitions) go in a single `app.css` per
module. No inline `style=` attributes.

### CSS custom properties for all values

All colours, spacing, font sizes, border radii, transition durations, and z-index values are
defined as **CSS custom properties (variables)** on `:root` in the module's `app.css`.
Component-scoped overrides use `::deep` only when the component genuinely owns a child
element's appearance.

```css
/* Good */
.chat-message { color: var(--color-chat-text); gap: var(--spacing-sm); }

/* Bad — hard-coded values */
.chat-message { color: #e0e0e0; gap: 8px; }
```

Variable naming convention: `--{category}-{name}[-{variant}]`
e.g. `--color-platform-twitch`, `--spacing-md`, `--radius-card`, `--duration-flash`.

### Glassmorphism design language

All three modules use a **glassmorphic** visual style: semi-transparent frosted panels
layered over a dark ambient background. This keeps the UI recessive and unobtrusive during
a live stream while remaining readable on the Surface Pro in a bright room.

#### Core rules

| Property | Rule |
|---|---|
| Background | `rgba` dark fill at low opacity (10–20 %) + `backdrop-filter: blur(…)` |
| Border | 1 px solid `rgba(255,255,255, 0.08–0.15)` — top/left edges only for depth |
| Box shadow | Outer glow with a very low-opacity dark shadow; no hard drop shadows |
| Border radius | Consistent rounded corners via `var(--radius-panel)` (default `12px`) |
| Text | High-contrast white/light on all glass surfaces; never dark-on-glass |
| Layering | Background layer → glass panels → content → flash/attention overlays |

#### Variables (defined in each module's `app.css`)

```css
:root {
  /* Glass surfaces */
  --glass-bg:              rgba(255, 255, 255, 0.06);
  --glass-bg-hover:        rgba(255, 255, 255, 0.10);
  --glass-border:          rgba(255, 255, 255, 0.10);
  --glass-blur:            blur(12px);
  --glass-shadow:          0 4px 24px rgba(0, 0, 0, 0.40);

  /* Accent glows (platform colours used sparingly) */
  --glow-live:             0 0 16px rgba(255, 60, 60, 0.45);
  --glow-ready:            0 0 16px rgba(60, 220, 100, 0.45);
}
```

#### Usage pattern

```css
/* component.razor.css — a standard glass panel */
.panel {
  background:       var(--glass-bg);
  backdrop-filter:  var(--glass-blur);
  border:           1px solid var(--glass-border);
  border-radius:    var(--radius-panel);
  box-shadow:       var(--glass-shadow);
}
```

`backdrop-filter` requires that the element has **no opaque ancestor** between it and the
background layer — keep the module page background set via `background-image` or a fixed
`<div class="bg">` rather than `background-color: #000` on `<body>`.

### Component wrapper architecture

Blazor's built-in form components (`EditForm`, `InputText`, `InputCheckbox`, etc.) are
**not used**. All markup is plain HTML elements. Styling consistency is achieved by wrapping
every reusable primitive in a thin Blazor component that renders the HTML element with the
correct CSS classes and wiring.

#### Rules

- All primitive components live in **`Thiccdal.Shared.Components/Components/Primitives/`**.
- Every input-style primitive uses `<InputContainer>` as its root element. `InputContainer` owns the chrome: outer `div`, label, and layout. The primitive places its raw HTML element inside `InputContainer`'s `ChildContent`.
- Use raw HTML tags (`<input>`, `<button>`, `<select>`, `<textarea>`) inside wrappers — never directly in pages or feature components.
- Each wrapper accepts a `Value` + `ValueChanged` pair for two-way binding, plus HTML attribute passthrough via `[Parameter(CaptureUnmatchedValues = true)]`.
- Wrappers do **not** implement `InputBase<T>` or any Blazor form infrastructure.
- `InputContainer` has an `Inline` mode (label to the right) used by `CheckBox` and `ToggleSwitch`.

#### `InputContainer`

The shared chrome wrapper. All input primitives use it as their root:

```razor
@* InputContainer.razor — renders label + ChildContent *@
<div class="@ContainerClass" @attributes="AdditionalAttributes">
    @if (!string.IsNullOrEmpty(Label) && !Inline) { <label class="input-label">@Label</label> }
    @ChildContent
    @if (!string.IsNullOrEmpty(Label) && Inline)  { <label class="input-label">@Label</label> }
</div>
```

| Parameter | Type | Purpose |
|---|---|---|
| `Label` | `string?` | Text shown above (stacked) or to the right (inline) of the control |
| `Inline` | `bool` | When `true`: row layout, label after control — used by `CheckBox` and `ToggleSwitch` |
| `ChildContent` | `RenderFragment?` | The actual input element |

#### Primitive component catalogue

| Component | Renders | Key parameters |
|---|---|---|
| `<TextBox>` | `<input type="text">` or `<textarea>` | `Value`/`ValueChanged`; `Label`; `Multiline` |
| `<NumberBox>` | `<input type="number">` | `Value`/`ValueChanged`; `Label`; optional `Min`/`Max`/`Step` |
| `<CheckBox>` | `<input type="checkbox">` | `Checked`/`CheckedChanged`; `Label` (inline, right of control) |
| `<ToggleSwitch>` | `<input type="checkbox">` + CSS pill | `Checked`/`CheckedChanged`; `Label` (inline, right of control) |
| `<SelectBox>` | `<select>` + `<option>` | `Value`/`ValueChanged`; `Label`; `Items` (`IEnumerable<SelectOption>`) |
| `<PrimaryButton>` | `<button type="button">` | `ChildContent`; `OnClick` |
| `<GhostButton>` | `<button type="button">` | `ChildContent`; `OnClick` |
| `<DangerButton>` | `<button type="button">` | `ChildContent`; `OnClick` |
| `<Panel>` | `<div>` | `ChildContent`; optional `Title` header |
| `<Badge>` | `<span>` | `ChildContent`; `Color` (`default`\|`success`\|`warning`\|`danger`\|`info`\|`live`\|`connected`\|`pending`) |

`SelectOption` is a record `(string Value, string Label)` in `Thiccdal.Shared.Components.Models`.

#### Example: `<TextBox>`

```razor
@* TextBox.razor — InputContainer is the root *@
<InputContainer Label="@Label">
    @if (Multiline)
    {
        <textarea class="input" value="@Value"
                  @oninput="e => ValueChanged.InvokeAsync(e.Value?.ToString())"
                  @attributes="AdditionalAttributes"></textarea>
    }
    else
    {
        <input type="text" class="input" value="@Value"
               @oninput="e => ValueChanged.InvokeAsync(e.Value?.ToString())"
               @attributes="AdditionalAttributes" />
    }
</InputContainer>

@code {
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public bool Multiline { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
}
```

Feature components consume wrappers only — never bare inputs:

```razor
@* Good *@
<TextBox Label="Stream title" @bind-Value="_title" />
<TextBox Label="Description" @bind-Value="_desc" Multiline="true" />

@* Bad — bare input leaks styling responsibility into the feature component *@
<input type="text" @bind="_title" class="..." />

---

## 7. Implementation Roadmap

Steps are numbered. **Major** headings represent cohesive phases; sub-steps are individually
shippable and testable. Items marked *(stretch)* are planned but not in the first iterations.

---

### Phase 1 — Foundation

| # | Step | Notes |
|---|---|---|
| 1.1 | Create full solution/directory structure | All projects, folders, slnx references |
| 1.2 | Add Aspire `AppHost` and `ServiceDefaults` projects | Wire Thiccdal as a resource |
| 1.3 | Create `Thiccdal.Infrastructure` project | Interfaces, enums, value types only |
| 1.4 | Create `Thiccdal.Data` project | EF Core, `ApplicationDbContext`, entities, migrations |
| 1.5 | Configure EF Core with SQLite in `Thiccdal.Data`; scaffold first migration | |
| 1.6 | Implement `IOptions<T>` configuration bindings | All option classes wired in DI |
| 1.7 | Add xUnit test project `Thiccdal.Data.Tests` | InMemory DB helper; base test fixtures |
| 1.8 | Set up GitHub Actions CI | Build + test on PR |

---

### Phase 2 — Platform Abstraction & Null Target

| # | Step | Notes |
|---|---|---|
| 2.1 | Define `IPlatformConnection`, `IChatService`, `IStreamTarget`, `IEventSource` | In `Thiccdal.Infrastructure` |
| 2.2 | Define `PlatformEvent` base record and `ChatMessage` record in `Thiccdal.Data` | Discriminator strategy in EF |
| 2.3 | Define `PlatformUser` entity in `Thiccdal.Data`; add migration | Chat messages link to this |
| 2.4 | Create `Thiccdal.Remote.Null` project | References `Thiccdal.Infrastructure` only; logs all ops |
| 2.5 | Unit-test the Null platform | Verify every interface method is called and logged |

---

### Phase 3 — Event Persistence Pipeline

| # | Step | Notes |
|---|---|---|
| 3.1 | Create EF migrations for `PlatformEvent` TPH table | Discriminator column |
| 3.2 | Implement `EventPersistenceService` | Saves event before dispatch |
| 3.3 | Implement in-process event bus (`IEventBus`) | Publish/subscribe; no external broker |
| 3.4 | Wire Null platform events through the bus | Integration test: event arrives + is persisted |
| 3.5 | Add known event subtypes: Subscribe, Follow, Redeem, Raid | Records with typed properties |

---

### Phase 4 — Chat Aggregation Service

| # | Step | Notes |
|---|---|---|
| 4.1 | Implement `ChatAggregationService` | Subscribes to all active platforms |
| 4.2 | Persist every inbound `ChatMessage` | Linked to `PlatformUser` |
| 4.3 | Upsert `PlatformUser` on first message | Create or update display name |
| 4.4 | Expose `IObservable<ChatMessage>` for UI subscription | Or use `Channel<T>` |
| 4.5 | Unit test: messages from two Null platforms arrive merged | |

---

### Phase 5 — Twitch Integration

| # | Step | Notes |
|---|---|---|
| 5.1 | Add `Thiccdal.Remote.Twitch` project | Add TwitchLib.Client NuGet |
| 5.2 | Implement Twitch chat connect/disconnect | `IChatService` |
| 5.3 | Map Twitch chat messages to `ChatMessage` | |
| 5.4 | Map Twitch Sub/Resub/GiftSub events | `TwitchSubscribeEvent` record |
| 5.5 | Map Twitch Raid event | `TwitchRaidEvent` record |
| 5.6 | Map Twitch Channel Point Redeem event | `RedeemEvent` record |
| 5.7 | Map unrecognised Twitch events to base `PlatformEvent` | With `RawData` |
| 5.8 | Implement Twitch stream info API (set title, tags) | Via Helix API |
| 5.9 | Unit-test all event mappings | Input raw payload → expected record type |
| 5.10 | Add `Thiccdal.Remote.Twitch.Tests` project | |

---

### Phase 6 — YouTube Integration

| # | Step | Notes |
|---|---|---|
| 6.1 | Add `Thiccdal.Remote.YouTube` project | YouTube Data API v3 client |
| 6.2 | Implement YouTube live chat polling | API has no push; poll on interval |
| 6.3 | Map YouTube SuperChat and Membership events | |
| 6.4 | Map YouTube chat messages to `ChatMessage` | |
| 6.5 | Map unrecognised YouTube events to base `PlatformEvent` | |
| 6.6 | Implement YouTube broadcast info API (set title, description) | |
| 6.7 | Unit-test all event mappings | |

---

### Phase 7 — Discord Integration

| # | Step | Notes |
|---|---|---|
| 7.1 | Add `Thiccdal.Remote.Discord` project | Discord.Net NuGet |
| 7.2 | Implement Discord bot connect/disconnect | `IChatService` |
| 7.3 | Map Discord messages to `ChatMessage` | |
| 7.4 | Map Discord reactions/events to `PlatformEvent` | |
| 7.5 | Map Discord live-stream RTMP relay | `IStreamTarget` |
| 7.6 | Unit-test all event mappings | |

---

### Phase 7b — LinkedIn Integration

| # | Step | Notes |
|---|---|---|
| 7b.1 | Add `Thiccdal.Remote.LinkedIn` project | LinkedIn Marketing API client |
| 7b.2 | Implement `IStreamTarget` for LinkedIn Live RTMP | Stub; logs until API approved |
| 7b.3 | Implement `IChatService` stub | LinkedIn has no public live chat API; log all ops |
| 7b.4 | Implement `IEventSource` stub | Emit base `PlatformEvent` with `RawData` |
| 7b.5 | Add `LinkedInOptions` with `IsEnabled` flag | When false, adapter is registered but skips all I/O |
| 7b.6 | Mark LinkedIn as disabled in operator UI | Badge shows "pending approval" tooltip |
| 7b.7 | Unit-test that disabled adapter performs no I/O | Verify no HTTP calls; verify log entry |

---

### Phase 7c — Facebook Live Integration

| # | Step | Notes |
|---|---|---|
| 7c.1 | Add `Thiccdal.Remote.Facebook` project | Meta Graph API SDK / HTTP client |
| 7c.2 | Implement `IStreamTarget` — create `LiveVideo`, obtain RTMP ingest URL | `FacebookOptions`: PageId, AppId, AppSecret |
| 7c.3 | Implement `IChatService` — poll `/{live-video-id}/comments` | Send messages via Graph API post |
| 7c.4 | Map comment events to `ChatMessage` | |
| 7c.5 | Map reactions/follow events to `PlatformEvent` | |
| 7c.6 | Implement Facebook stream info API (title, description) | Via `LiveVideo` update endpoint |
| 7c.7 | Unit-test all event mappings | |

---

### Phase 7d — X (Twitter) Live Integration

| # | Step | Notes |
|---|---|---|
| 7d.1 | Add `Thiccdal.Remote.X` project | X API v2 HTTP client |
| 7d.2 | Implement `IStreamTarget` — create broadcast, obtain RTMP ingest URL | `XOptions`: ApiKey, ApiKeySecret, AccessToken, AccessTokenSecret |
| 7d.3 | Implement `IChatService` — poll Tweet replies via `search/recent` | Rate-limit aware |
| 7d.4 | Map reply events to `ChatMessage` | |
| 7d.5 | Map likes/reposts to `PlatformEvent` | |
| 7d.6 | Unit-test all event mappings and rate-limit handling | |

---

### Phase 7e — TikTok Live Integration *(disabled until API approved)*

| # | Step | Notes |
|---|---|---|
| 7e.1 | Add `Thiccdal.Remote.TikTok` project | TikTok Open API client |
| 7e.2 | Implement `IStreamTarget` stub | RTMP via TikTok Live Studio endpoint |
| 7e.3 | Implement `IChatService` — `live.comment_list` polling | |
| 7e.4 | Implement `IEventSource` — `live.event_list` (gifts, likes, follows) | |
| 7e.5 | Add `TikTokOptions` with `IsEnabled` flag | Same disabled pattern as LinkedIn |
| 7e.6 | Mark TikTok as disabled in operator UI | Badge shows "pending approval" tooltip |
| 7e.7 | Unit-test that disabled adapter performs no I/O | |

---

### Phase 8 — RTMP Multicast Server

| # | Step | Notes |
|---|---|---|
| 8.1 | Add `Thiccdal.Streaming` project | Choose RTMP library (e.g. Xabe.FFmpeg relay, LiveReacting, or custom) |
| 8.2 | Implement RTMP ingest listener | OBS pushes here |
| 8.3 | Implement fanout relay to `IStreamTarget` list | Concurrent; isolated failure per target |
| 8.4 | Detect ingest disconnect; inject BRB slate | FFmpeg static video loop or image |
| 8.5 | Persist relay session start/stop to `StreamRecording` | |
| 8.6 | Record to disk | Configurable output path and format |
| 8.7 | Unit-test relay lifecycle (connect, fanout, disconnect, BRB) | Mock IStreamTarget |

---

### Phase 9 — Chatbot

| # | Step | Notes |
|---|---|---|
| 9.1 | Add `BotCommand` and `ProactiveMessage` EF entities + migration | |
| 9.2 | Define `ICommandHandler` interface and `CommandContext` record | In `Thiccdal.Infrastructure` |
| 9.3 | Implement `CommandRegistry` | Loads from DB; caches; reloads on change |
| 9.4 | Implement `CommandDispatcher` | Parses `!trigger` prefix; resolves handler from DI if set |
| 9.5 | Implement metadata token interpolation | `{user}`, `{platform}`, `{count}`, `{uptime}` |
| 9.6 | Wire dispatcher into `ChatAggregationService` | |
| 9.7 | Implement proactive timer (`IHostedService`) | Per-`ProactiveMessage` interval from DB |
| 9.8 | Implement `!commands` meta-command | Built-in; lists enabled triggers |
| 9.9 | Build command management dialog in operator UI | CRUD on `BotCommand`; not shown on live screen |
| 9.10 | Unit-test dispatch (hit, miss, wrong args, handler override) | |
| 9.11 | Unit-test token interpolation | One `[Theory]` per token type |
| 9.12 | *(Stretch)* Wire Azure OpenAI / Ollama for free-form response | Behind feature flag; as `ICommandHandler` |

---

### Phase 10 — Command & Control UI (Thiccdal.Modules.Control)

| # | Step | Notes |
|---|---|---|
| 10.1 | Design and stub dual-mode layout (Pre-Live / Live) in `Thiccdal.Modules.Control` | CSS grid; Surface Pro target (~1366×912); isolated `.razor.css` per component |
| 10.2 | Implement mode state in `IOperatorStateService` | `PreLive` / `Live`; persists across page refreshes |
| 10.3 | Implement stream status badge (Pre-Live / Live ● uptime) | Top bar left |
| 10.4 | Implement per-platform connection status badges | Including disabled-platform states |
| 10.5 | Implement Go Live button with locked/ready/confirming states | Top bar right; disabled until checklist complete |
| 10.6 | Implement Go Offline button (live mode only) | Requires confirmation; stops all relays |
| 10.7 | Implement confirmation dialog component | Reusable; shows summary list of warnings if any |
| 10.8 | Implement combined chat feed component (live mode) | Left panel; scrollable; platform badge per message |
| 10.9 | Implement teleprompter scroll controls (live mode) | ▲ / ▼ buttons driving shared scroll state |
| 10.10 | Implement question queue panel (live mode) | Dismiss / Feature / Complete; synced across operators |
| 10.11 | Flash indicator on new question arrival | CSS animation on question queue panel header |
| 10.12 | Implement stream info quick-set panel (pre-live mode) | Sets title, category, tags; pushes to all platforms |
| 10.13 | Implement “Manage Commands” button + dialog | Opens chatbot command CRUD |
| 10.14 | Responsive layout for phone subset | Pre-live: status + checklist progress; live: status + queue |

---

### Phase 11 — Overlay & Lower Third (Thiccdal.Modules.Overlay)

| # | Step | Notes |
|---|---|---|
| 11.1 | Scaffold `Thiccdal.Modules.Overlay` project (Blazor RCL) | Referenced by main Blazor host; isolated `.razor.css` per component |
| 11.2 | Create `/overlay` route (no chrome, transparent background) | OBS browser source |
| 11.3 | Implement `IOverlayComponent` contract | Register/unregister pattern |
| 11.4 | Implement `ITestableOverlayComponent` extending `IOverlayComponent` | Adds test flash capability |
| 11.5 | Implement test flash animation | 3 s lime/green border pulse + “TESTING — [Name]” label; JS interop |
| 11.6 | Implement `ChatFeedOverlayComponent` (implements `ITestableOverlayComponent`) | Combined chat with platform badge |
| 11.7 | Implement `EventTickerOverlayComponent` (implements `ITestableOverlayComponent`) | Subscribes, follows, redeems |
| 11.8 | Implement `LowerThirdOverlayComponent` (implements `ITestableOverlayComponent`) | Driven by question queue |
| 11.9 | Implement prompter attention flash (new question) | CSS gradient sweep from right, cyan; JS interop |
| 11.10 | Implement prompter attention flash (significant event) | Same mechanism, gold accent |
| 11.11 | Implement manual settings reminders in Stream Info dialog | Hardcoded list; checklist UI |
| 11.12 | Write bUnit tests for each overlay component render | |

---

### Phase 12 — Status Endpoint & Online/Offline Badge

| # | Step | Notes |
|---|---|---|
| 12.1 | Define `StreamStatusResponse` record | `State`, `Stream` (nullable), `Platforms[]` |
| 12.2 | Add `GET /status` returning `StreamStatusResponse` JSON | Reads from `IOperatorStateService` |
| 12.3 | Populate `stream` object from active `StreamSession` | title, category, tags, startedAt, uptime |
| 12.4 | Populate `platforms[]` from all registered `IPlatformConnection` instances | name + state per adapter |
| 12.5 | Add `GET /status/badge.svg` returning image | Online/offline static asset |
| 12.6 | Unit-test `/status` JSON shape when online, offline, and partial failure | |
| 12.7 | Unit-test badge endpoint returns correct asset | |

---

### Phase 13 — Cross-Platform User Identity *(Stretch)*

| # | Step | Notes |
|---|---|---|
| 13.1 | Add `UserIdentity` aggregate linking multiple `PlatformUser` rows | Nullable FK |
| 13.2 | Implement manual merge UI in operator page | |
| 13.3 | *(Future)* Heuristic matching by display name similarity | |

---

### Phase 14 — Hardening & Observability

| # | Step | Notes |
|---|---|---|
| 14.1 | Add OpenTelemetry traces and metrics | Via Aspire ServiceDefaults; exclude health probes from request tracing and emit service metadata for Aspire dashboards/exporters |
| 14.2 | Add health-check endpoints | `/health` = liveness-only, `/ready` = full readiness including SQLite connectivity |
| 14.3 | Structured log review — remove noise, add missing context | Suppress EF command chatter, keep migration/readiness logs contextual |
| 14.4 | Add retry/backoff for platform API calls | Named platform `HttpClient` registrations own the resilience pipeline |
| 14.5 | Integration test pass: spin up full stack against Null platform | Verified via host-level WebApplicationFactory coverage |

---

### Phase 15 — Multi-Operator State Sync

| # | Step | Notes |
|---|---|---|
| 15.1 | Define `IOperatorStateService` and singleton implementation | In `Thiccdal.Infrastructure` |
| 15.2 | Add `StateChanged` event to `IOperatorStateService` | Fired on any state mutation |
| 15.3 | Move question queue state into `IOperatorStateService` | Replace component-local state |
| 15.4 | Move teleprompter scroll position into `IOperatorStateService` | |
| 15.5 | Move lower-third visibility into `IOperatorStateService` | |
| 15.6 | Subscribe each operator component to `StateChanged` | `InvokeAsync(StateHasChanged)` pattern |
| 15.7 | Subscribe `/prompter` and `/overlay` to `StateChanged` | Same pattern |
| 15.8 | Integration test: two simulated circuits see same state | |
| 15.9 | Manual test: two browser tabs drive the same instance | Verify no auth, full sync |

---

### Phase 16 — Pre-Live Checklist

| # | Step | Notes |
|---|---|---|
| 16.1 | Define `ChecklistItemDefinition` record and `ChecklistItemState` record | In `Thiccdal.Infrastructure` |
| 16.2 | Define `IPreLiveChecklistService` interface | In `Thiccdal.Infrastructure` |
| 16.3 | Implement `PreLiveChecklistService` singleton | Holds all item states; publishes to `IOperatorStateService` |
| 16.4 | Implement Platform Connections category (auto-check monitors) | Subscribes to `IPlatformConnection.State` changes |
| 16.5 | Implement Stream Info category (auto-checks + action trigger) | Auto-checks title/category; action opens stream info panel |
| 16.6 | Implement OBS & Technical category (manual items) | Hard-coded definitions; shows ingest URL inline |
| 16.7 | Implement Overlay Verification category (action items) | Enumerates `ITestableOverlayComponent` registrations dynamically |
| 16.8 | Implement Recording category (auto-with-warn) | Polls path config + disk space |
| 16.9 | Implement Personal Prep category (manual, optional) | Loads custom items from `CustomChecklistItem` DB entity |
| 16.10 | Implement CRUD UI for custom personal prep items | Dialog; stored in DB |
| 16.11 | Implement Go Live button enabled/disabled logic | Reads `IPreLiveChecklistService.AllRequiredChecked` |
| 16.12 | Implement Go Live confirmation dialog | Shows checklist summary; lists unchecked optional items as warnings |
| 16.13 | Implement Go Live action | Transitions mode to `Live`; starts RTMP relay to all enabled platforms |
| 16.14 | Add `ChecklistSession` EF entity + migration | Persists final item states per stream session |
| 16.15 | Unit-test auto-check logic for each auto/auto-with-warn item | |
| 16.16 | Unit-test Go Live button disabled until all required items checked | |
| 16.17 | Integration test: Go Live action starts relay and transitions mode |

---

## 7. GitHub Copilot Skills, Agents & Prompt Catalogue

All skills and agents below are from [github/awesome-copilot](https://github.com/github/awesome-copilot) —
the official curated collection maintained by GitHub.

### Recommended Skills

Skills enhance Copilot for specific tasks. Install via the Copilot CLI:

```bash
copilot plugin marketplace add github/awesome-copilot
copilot plugin install <skill-id>@awesome-copilot
```

Or open the linked `SKILL.md` and add its contents to `.github/copilot-instructions.md`.

| Priority | Skill | What it does for Thiccdal |
|---|---|---|
| ★★★ | [`aspire`](https://github.com/github/awesome-copilot/blob/main/skills/aspire/SKILL.md) | Aspire CLI, AppHost orchestration, service discovery, integrations — covers everything in Phases 1–2 |
| ★★★ | [`dotnet-best-practices`](https://github.com/github/awesome-copilot/blob/main/skills/dotnet-best-practices/SKILL.md) | Validates C# code against .NET best practices; enforces the conventions in this repo |
| ★★★ | [`ef-core`](https://github.com/github/awesome-copilot/blob/main/skills/ef-core/SKILL.md) | EF Core best practices; migrations, TPH discriminators, `DbContext` lifetimes |
| ★★★ | [`csharp-async`](https://github.com/github/awesome-copilot/blob/main/skills/csharp-async/SKILL.md) | C# async best practices — critical given the whole codebase is async by convention |
| ★★★ | [`csharp-xunit`](https://github.com/github/awesome-copilot/blob/main/skills/csharp-xunit/SKILL.md) | xUnit best practices including data-driven tests; used project-wide |
| ★★★ | [`nuget-manager`](https://github.com/github/awesome-copilot/blob/main/skills/nuget-manager/SKILL.md) | Add/update NuGet packages (TwitchLib, Discord.Net, EF Core, etc.) without leaving chat |
| ★★★ | [`microsoft-code-reference`](https://github.com/github/awesome-copilot/blob/main/skills/microsoft-code-reference/SKILL.md) | Look up .NET API signatures, working samples, and SDK correctness |
| ★★★ | [`refactor`](https://github.com/github/awesome-copilot/blob/main/skills/refactor/SKILL.md) | Surgical refactoring without behaviour change; useful when splitting layers or extracting services |
| ★★☆ | [`fluentui-blazor`](https://github.com/github/awesome-copilot/blob/main/skills/fluentui-blazor/SKILL.md) | Fluent UI Blazor component library guidance; relevant for the touch-friendly operator UI |
| ★★☆ | [`dotnet-design-pattern-review`](https://github.com/github/awesome-copilot/blob/main/skills/dotnet-design-pattern-review/SKILL.md) | Reviews C#/.NET code for design pattern use; flags anti-patterns |
| ★★☆ | [`review-and-refactor`](https://github.com/github/awesome-copilot/blob/main/skills/review-and-refactor/SKILL.md) | Combined code review + refactor pass against defined project standards |
| ★★☆ | [`webapp-testing`](https://github.com/github/awesome-copilot/blob/main/skills/webapp-testing/SKILL.md) | Playwright-based testing of `/overlay` and `/prompter` as they run in a real browser |
| ★★☆ | [`web-design-reviewer`](https://github.com/github/awesome-copilot/blob/main/skills/web-design-reviewer/SKILL.md) | Visual inspection of running pages; validates touch-friendly layout on Surface Pro form factor |
| ★★☆ | [`git-commit`](https://github.com/github/awesome-copilot/blob/main/skills/git-commit/SKILL.md) | Conventional commit messages with intelligent staging |
| ★★☆ | [`github-issues`](https://github.com/github/awesome-copilot/blob/main/skills/github-issues/SKILL.md) | Create/update GitHub issues directly from chat — bug reports, feature requests |
| ★★☆ | [`update-specification`](https://github.com/github/awesome-copilot/blob/main/skills/update-specification/SKILL.md) | Keeps this architecture doc in sync as new requirements are decided |
| ★☆☆ | [`csharp-docs`](https://github.com/github/awesome-copilot/blob/main/skills/csharp-docs/SKILL.md) | Ensures public types have XML doc comments |
| ★☆☆ | [`prd`](https://github.com/github/awesome-copilot/blob/main/skills/prd/SKILL.md) | Generate a PRD for a new feature before starting implementation |
| ★☆☆ | [`sql-optimization`](https://github.com/github/awesome-copilot/blob/main/skills/sql-optimization/SKILL.md) | SQLite query tuning and index strategy for the `ApplicationDbContext` |
| ★☆☆ | [`appinsights-instrumentation`](https://github.com/github/awesome-copilot/blob/main/skills/appinsights-instrumentation/SKILL.md) | Add Azure App Insights telemetry if cloud monitoring is needed |

---

### Recommended Agents

Agents are persistent Copilot personas activated for a session. Install via the VS Code badge
in each agent's file, or add the `.agent.md` content to your Copilot workspace settings.

| Priority | Agent | When to use |
|---|---|---|
| ★★★ | [`Expert .NET software engineer`](https://github.com/github/awesome-copilot/blob/main/agents/expert-dotnet-software-engineer.agent.md) | Activate for any substantial feature implementation — enforces SOLID, async, DI patterns |
| ★★★ | [`C#/.NET Janitor`](https://github.com/github/awesome-copilot/blob/main/agents/csharp-dotnet-janitor.agent.md) | Clean up, modernize, and reduce tech debt across existing C# code |
| ★★☆ | [`Context Architect`](https://github.com/github/awesome-copilot/blob/main/agents/context-architect.agent.md) | Plan and execute multi-file changes — useful when scaffolding a new platform adapter or phase |
| ★★☆ | [`Debug Mode`](https://github.com/github/awesome-copilot/blob/main/agents/debug-mode.agent.md) | Focused debugging session for a specific failing test or runtime error |
| ★★☆ | [`DevOps Expert`](https://github.com/github/awesome-copilot/blob/main/agents/devops-expert.agent.md) | CI/CD pipeline, GitHub Actions, containerisation, and deployment questions |
| ★☆☆ | [`Critical Thinking Mode`](https://github.com/github/awesome-copilot/blob/main/agents/critical-thinking-mode.agent.md) | Challenge an architecture decision or approach before committing to it |

> **Note:** Agent file names in awesome-copilot may differ from display names.
> Browse [agents/](https://github.com/github/awesome-copilot/tree/main/agents) to find the exact `.agent.md` file.

---

### Built-in Copilot Chat Commands

| Command | When to use |
|---|---|
| `/fix` | Fix a compiler error or test failure Copilot has context on |
| `/explain` | Understand an existing class or algorithm |
| `/tests` | Generate xUnit tests for a selected class or method |
| `/doc` | Generate XML doc comments for a public API |
| `@workspace` | Ask a question that spans multiple files |

### File Reference Shortcuts

Prefix prompts with these for precise context:

```
#file:src/Thiccdal.Infrastructure/Remotes/IPlatformConnection.cs
#file:src/Thiccdal.Data/ApplicationDbContext.cs
#file:src/Thiccdal.Infrastructure/Bot/Models/PlatformEvent.cs
#file:src/Remote/Thiccdal.Remote.Null/NullPlatformConnection.cs
```

### Reusable Prompt Templates

#### Scaffold a new platform adapter
```
Using #file:src/Thiccdal.Infrastructure/Remotes/IPlatformConnection.cs,
scaffold Thiccdal.Remote.<Platform> under src/Remote/ implementing IPlatformConnection,
IChatService, IStreamTarget and IEventSource. Use IOptions<<Platform>Options> for
configuration. Add an EventMapper class with a unit-test in
src/Tests/Remote/Thiccdal.Remote.<Platform>.Tests covering at least one known event type
and the unknown-event fallback.
```

#### Add a new domain event type
```
Using #file:src/Thiccdal.Infrastructure/Bot/Models/PlatformEvent.cs as the base,
add a new record <Platform><Name>Event with these properties: <list>.
Register the discriminator value in #file:src/Thiccdal.Data/ApplicationDbContext.cs.
Add it to #file:src/Remote/Thiccdal.Remote.<Platform>/EventMapper.cs.
Write a [Fact] test: given raw payload X, mapper returns correct typed event.
```

#### Add a new overlay component
```
Using #file:src/Modules/Thiccdal.Modules.Overlay/IOverlayComponent.cs,
create <Name>OverlayComponent.razor and <Name>OverlayComponent.razor.css.
Register it in OverlayComponentRegistry.
Write a bUnit [Fact] test verifying the rendered markup when given a sample input model.
```

#### Add a new chatbot command handler
```
Using #file:src/Thiccdal.Infrastructure/Bot/ICommandHandler.cs as the contract,
scaffold a new <Name>CommandHandler that can be wired to a BotCommand row via its HandlerType column.
Inject any dependencies via DI constructor injection.
Write two [Fact] tests: happy-path response and an error/edge-case path.
```

#### Generate EF migration
```
The following entities changed: <list changes>.
Generate an EF Core migration named <MigrationName> using
#file:src/Thiccdal.Data/ApplicationDbContext.cs.
Verify the Up() method handles nullability and index correctness.
```

#### Debug a failing test
```
This test is failing: #file:<path to test file>
The service under test is #file:<path to implementation>.
Explain why it fails and suggest the minimal fix, following the conventions in
#file:.github/copilot-instructions.md.
```

#### Review a PR for conventions
```
Review the changes in this PR against the conventions defined in
#file:.github/copilot-instructions.md.
Flag any: magic-string config access, missing CancellationToken, async void,
Async suffix on method names, public members without justification,
missing interface in Thiccdal.Infrastructure, concrete type injected instead of interface,
unresolved warnings, or tests that mock internal code.
```

---

## 8. Key Non-Functional Requirements

| Concern | Decision |
|---|---|
| Latency | Chat and event display target < 500 ms end-to-end from platform to overlay |
| Resilience | Each platform adapter failure is isolated; other platforms continue unaffected |
| Security | Stream keys and OAuth tokens are never logged; stored only in env vars / secrets |
| Portability | Cross-platform .NET; no Windows-specific APIs without a guard |
| Offline dev | Null platform allows full UI/UX development without live credentials |
| Data retention | All chat, events, and recordings are retained indefinitely by default; pruning is a future feature |

---

## 9. Open Questions & Future Considerations

- **LinkedIn RTMP** — Adapter is built and disabled. Enabling requires LinkedIn Live API approval and adding credentials to config. No code changes needed.
- **TikTok Live** — Same as LinkedIn: built and disabled. Requires TikTok Open API approval.
- **X (Twitter) API tiers** — X API pricing and tier structure changes frequently. Verify write-access requirements at implementation time.
- **Facebook App Review** — `live_video` and `pages_manage_posts` permissions require Meta App Review for production.
- **AI chatbot** — Azure OpenAI or a local Ollama instance; feature-flagged via a `ICommandHandler` implementation; not in MVP.
- **Mobile control** — Phone subset requires breakpoint design work; deprioritised after Surface Pro.
- **User identity matching** — Cross-platform heuristics deferred to stretch phase (see Phase 13).
- **Authentication** — Explicitly out of scope for v1. All operator sessions are trusted.
- **Clip creation** — Triggering platform clip creation from the operator UI is a future feature.
- **Moderation actions** — Ban/timeout from operator UI is a future feature.
- **Trovo / Vimeo Livestream** — Legitimate platforms worth adding in a future iteration; not roadmapped yet.
- **Command token extensibility** — New interpolation tokens (e.g. `{title}`, `{game}`) can be added to the token resolver without changing the DB schema.

---

*Last updated: initial draft*
