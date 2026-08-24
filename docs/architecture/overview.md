# Thiccdal – Architecture Overview

> **Status:** Living document. Updated as decisions are made and features are built.
> An `/architecture/` folder is reserved for individual Architectural Decision Records, but no
> ADRs have been written yet — this document is currently the single source of truth.

---

## 1. Purpose

Thiccdal is a streaming command-and-control system. It runs on a stream PC and is operated
from a separate device (e.g., a Surface Pro tablet via browser). A single operator interface
gives full visibility and control of platform connections, chat, events, and overlays —
without needing to switch between apps or screens.

### Scope boundaries

Two constraints shape everything below. Both are deliberate; treat them as invariants.

**Twitch is the only platform.** The adapter architecture stays modular — `IPlatformConnection`,
one project per platform under `/src/Remote/` — so another platform could be added later. Until
that decision is made, no code, configuration, or documentation refers to YouTube, Discord,
Facebook, X, TikTok, or LinkedIn.

**Video is out of scope.** Thiccdal never ingests, restreams, records, or otherwise touches the
video pipeline. OBS publishes to Twitch directly and Thiccdal never sits between them. There is
no RTMP ingest, no fanout, no relay, no disk recording, and no stream-key handling. Thiccdal's
relationship with OBS is read-only telemetry over obs-websocket (§3.13).

---

## 2. High-Level Architecture

OBS publishes video to Twitch on its own. Thiccdal runs alongside it on the same machine,
handling chat, events, overlays, and operator control.

```
   OBS Studio ──────── RTMP video ────────▶ Twitch
       │  ▲                                   │
       │  │ obs-websocket (stream state)      │ chat + EventSub
       │  │ browser source / browser dock     │
       ▼  │                                   ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                         Stream PC (Server)                               │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  Thiccdal (Blazor Server host)                                   │   │
│  │   ├── Thiccdal.Modules.Control      (operator UI, touch-friendly)│   │
│  │   ├── Thiccdal.Modules.Teleprompter (combined chat + events)     │   │
│  │   ├── Thiccdal.Modules.Overlay      (SignalR → OBS browser src)  │   │
│  │   ├── Thiccdal.Modules.ChatBot      (aggregation + commands)     │   │
│  │   └── Thiccdal.API                  (status + Stream Deck)       │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  Thiccdal.Infrastructure                                         │   │
│  │   └── Interfaces, options, value types — no EF Core              │   │
│  │       (IPlatformConnection, IChatService, IObsConnection, …)     │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  Thiccdal.Data                                                   │   │
│  │   ├── ApplicationDbContext  (SQLite / EF Core)                   │   │
│  │   ├── Entity Models         (ChatMessage, PlatformEvent, …)      │   │
│  │   └── Migrations                                                 │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Remote Adapters                                                  │  │
│  │   ├── Thiccdal.Remote.Twitch    chat, EventSub, Helix, OAuth      │  │
│  │   ├── Thiccdal.Remote.Obs       obs-websocket stream state        │  │
│  │   ├── Thiccdal.Remote.LMStudio  local LLM for AI features         │  │
│  │   └── Thiccdal.Remote.Null      logging-only; used in tests       │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
                          ▲                  ▲
             Blazor Server │                 │  Overlay browser source
             circuit (WS)  │                 │  and teleprompter dock
                    ┌──────┴───────┐   ┌─────┴────────────┐
                    │ Control UI   │   │ Overlay Page      │
                    │ (Surface Pro)│   │ Prompter Page     │
                    │ multi-client │   │ (inside OBS)      │
                    └──────────────┘   └──────────────────┘
```

### Project & Module Layout

Directory structure on disk mirrors the solution structure. The solution file is
`Thiccdal.slnx` (XML solution format) at the repo root.

```
/src/Thiccdal/                          Blazor Server host (surfaces, /config pages, layouts)
/src/Thiccdal.Infrastructure/           Interfaces, options, enums, value types — no EF Core
  AI/                                     AI abstractions shared by the AI project
  Actions/                                Operator quick-action contracts
  Bot/                                    IChatService, ICommandDispatcher, chat event models
    Models/                               ChatEvent, PlatformEvent, RawEvent, PlatformEventSource
  Integrations/                           IIntegrationConnectionMonitor
  LmStudio/                               LmStudioOptions and local-LLM contracts
  Operators/                              Operator state, pre-live checklist, go-live action
  Overlay/                                IOverlayService, IOverlayComponent
  Questions/                              Question queue and detection contracts
  Readiness/                              ISystemReadinessService, SystemReadiness
  Remotes/                                IPlatformConnection, IChatSource, IEventBus
  Setup/                                  IConfigurationPersistenceService (settings store)
  Sponsors/                               ISponsorshipService
  Streaming/                              IObsConnection, ObsState, ObsOptions
  Teleprompter/                           ITeleprompterService, ScrollDirection, ScrollEventArgs
  Twitch/                                 ITwitchService, ITwitchTokenManager, TwitchOptions
/src/Thiccdal.Data/                     EF Core DbContext, entities, migrations
  Models/                                 Entity classes (e.g. TwitchToken)
  Migrations/                             EF Core migration files
/src/Thiccdal.API/                      Minimal API endpoint extensions
  Status/                                 Online/offline status endpoint (§3.9)
  StreamDeck/                             Stream Deck control endpoints
/src/Thiccdal.AI/                       AI/LLM services
/src/Modules/
  Thiccdal.Modules.ChatBot/             Chat aggregation + command dispatch (Razor Class Library)
    Services/
  Thiccdal.Modules.Control/             Streamer dashboard (Razor Class Library)
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
      Readiness/                           ReadinessGate surface gating
    Models/                                SelectOption and other shared data types
/src/Remote/
  Thiccdal.Remote.Twitch/                chat, EventSub, Helix, OAuth
  Thiccdal.Remote.Obs/                   obs-websocket client for OBS Studio
  Thiccdal.Remote.LMStudio/              local LLM client for AI features
  Thiccdal.Remote.Null/                  logging-only; used in tests
/src/Aspire/
  Thiccdal.Aspire.AppHost/              Aspire AppHost
  Thiccdal.Aspire.ServiceDefaults/      Aspire ServiceDefaults
/src/Tests/                             Mirrors the source tree
  Thiccdal.Tests/                        Host and Infrastructure tests
  Thiccdal.Data.Tests/                   Data layer tests
  Thiccdal.AI.Tests/                     AI service tests
  Modules/
    Thiccdal.Modules.ChatBot.Tests/
    Thiccdal.Modules.Teleprompter.Tests/
  Remote/
    Thiccdal.Remote.Twitch.Tests/
    Thiccdal.Remote.Obs.Tests/
/docs/architecture/                     Architecture .md files
/docs/help/                             End-user documentation
/architecture/                          Architectural Decision Records (reserved; empty today)
```

A test project that is not listed in `Thiccdal.slnx` is never built or run. If you add one, add
it to the solution in the same commit.

---

## 3. Feature Descriptions

### 3.0 Surfaces

Thiccdal presents four separate surfaces. Each has its own layout, its own input model, and its
own audience. They are not variations of one page.

| Surface | Route | Input | Purpose |
|---|---|---|---|
| Streamer dashboard | `/dashboard` | Touch | Instant control while live. **No setup lives here.** |
| Teleprompter | `/prompter` | Touch / read-only | On-camera script and chat |
| Overlay | `/overlay` | — | OBS browser source |
| Configuration | `/config` | Keyboard + mouse, large screen | Everything else |

`/config` has two sections: **Bot** (`/config/bot/*` — commands, autoresponses, identity and
greetings) and **System** (`/config/system/*` — Twitch, AI keys, AI memory, viewer identities,
pre-live checklist, appearance). There is no setup wizard; `/config` is the single configuration
surface, and `/` redirects to it because the root has no surface of its own.

**Adding an editing affordance to the dashboard or teleprompter is a mistake** — it belongs in
`/config`. Components shared between the two take an `Inline` parameter that drops the modal
chrome (see `BotCommandManagementDialog`, `PersonalPrepManageDialog`).

#### Readiness gating

`ISystemReadinessService` (`Thiccdal.Infrastructure/Readiness/`) reports what is configured.
Gated surfaces wrap themselves in `<ReadinessGate>`:

| Surface | Requirement |
|---|---|
| Teleprompter | A saved Twitch channel |
| Streamer dashboard | A saved channel **and** an authorized Twitch account |

Until then each shows an unconfigured notice pointing at `/config`, and activates automatically
once the requirement is met — no restart, because readiness changes raise `ReadinessChanged` and
the circuit re-renders.

### 3.1 Platform Connection Abstraction

Every platform adapter implements `IPlatformConnection`. Today that is Twitch and Null; the
interface exists so a second platform can be added without reworking the consumers.

| Member | Responsibility |
|---|---|
| `IChatSource` (base) | Send and receive chat messages |
| `PlatformName` | Display name used by status and operator surfaces |
| `State` | Normalised `PlatformConnectionState` |
| `LastError` | Platform error message when `State` is `Error` |
| `RefreshConnectionState` | Re-reads auth or transport state |

Implementations are resolved through DI and never referenced by concrete type outside their own
project. The `Null` implementation logs every operation at `Information` level and emits no
traffic; it is the default in unit tests and suitable for offline development.

> `IPlatformConnection` also inherits `IStreamTarget`, which is an empty marker interface left
> over from the removed restreaming feature. It carries no members and no implementation does
> anything with it.

Platform auth state reaches the UI through `IIntegrationConnectionMonitor`
(`Thiccdal.Infrastructure/Integrations/`) rather than through `IPlatformConnection` directly, so
components can render any platform's connection status without knowing which platform it is.

### 3.2 Event System

All platform happenings — subscribes, follows, redeems, raids, cheers — derive from
`PlatformEvent`. Known event types have dedicated record types carrying additional properties
(for example `TwitchSubscribeEvent`). Unrecognised events emit the base `PlatformEvent` with a
`RawData` field holding the raw platform payload, so nothing is silently discarded.

**Rule:** Every event is persisted to the database *before* it is dispatched to subscribers.
This guarantees a full audit trail regardless of downstream handler failures.

When normalising a batched or polled payload, store the serialized **item** payload and the
source event name — not the batch envelope. `PlatformUserIdResolver` reads item-level raw data,
and an envelope silently breaks identity resolution.

### 3.3 Chat Aggregation & User History

All incoming chat messages are normalised to a `ChatMessage` record and persisted. Each message
is linked to a `PlatformUser` record scoped to its platform.

`UserIdentity` and `UserIdentitySuggestion` entities exist to link one person's accounts
together. With Twitch as the only platform there is nothing to correlate across, so the
scaffolding is in place but the feature is dormant.

### 3.4 Chatbot

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
| `{platform}` | Platform name (Twitch) |
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

### 3.5 Overlay (Thiccdal.Modules.Overlay)

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

When triggered from the Pre-Live Checklist (see §3.12), the component displays a prominent
full-component overlay for 3 seconds showing **“■ TESTING — [Component Name]”** in large
bold text with a lime/green border pulse. This is deliberately more visible and persistent
than the event flash (see §3.6), because its purpose is confirmation that the overlay is
correctly positioned and visible in OBS, not just a brief notification.

The test is triggered via the same `IOperatorStateService` state mechanism so it fires on all
connected sessions simultaneously (useful when one operator is watching OBS while another
operates the control device).

### 3.6 Teleprompter (Thiccdal.Modules.Teleprompter)

A full-screen page (`/prompter`) showing the combined event and chat feed in large, readable
text. Lives in the `Thiccdal.Modules.Teleprompter` Razor Class Library and is hosted by the
main Blazor Server app. **The teleprompter itself has no interactive controls** — it is a
passive display intended to be shown on a second monitor or screen facing the streamer.

#### Hosting the prompter view
The prompter is displayed as an **OBS custom browser dock** pointed at `http://<host>/prompter`,
floated onto the streamer's reading monitor. There is no companion desktop application: a
WebView2 shell (`Thiccdal.Teleprompter.Display`) previously wrapped this same page to add monitor
placement, click-through, a global hotkey, and an obs-websocket client. The OBS dock supplies the
window management for free, the control device supplies the interaction, and the obs-websocket
client moved into the host (see §3.13) — so the shell was removed.

Because the OBS browser engine cannot be taught to trust the development certificate, the host
applies **neither `UseHttpsRedirection` nor HSTS**. Every surface is reachable over HTTP and
HTTPS alike; reintroducing either middleware breaks the dock.

Scrolling is driven entirely from the **Command & Control UI** via shared state. The operator
taps up/down scroll buttons on their control device (Surface Pro) and the prompter view reacts
in real time through the same multi-operator state sync mechanism (see §3.10).

#### Prompter attention flash
Because the prompter is the streamer’s primary on-screen reference, **the prompter page itself
flashes** when something requires attention. Two triggers:

| Trigger | Flash style |
|---|---|
| New question added to the queue | Gradient sweep from the right edge, ~0.5 s, cyan/teal accent |
| Significant platform event (subscribe or raid) | Gradient sweep from the right edge, ~0.5 s, gold accent |

The flash is implemented as a CSS animation (`@keyframes`) applied to a fixed-position overlay
div injected by JS interop. It auto-dismisses and does not obstruct the prompter text.

The control UI shows its own separate flash indicator for new questions (in the question queue
panel header — see §3.7), independent of the prompter flash.

### 3.7 Question Queue

Questions posted in any chat are flagged (manually or by a bot command). The queue is displayed
in the operator UI. The operator can:

1. **Dismiss** — remove from queue silently, without showing to viewers.
2. **Feature** — push to the lower-third overlay for viewers to see.
3. **Complete** — marks the question as handled: removes it from the overlay lower-third
   *and* removes it from the queue. Communicates clearly that the question has been addressed,
   not discarded.

All three state transitions are synced across all connected operator sessions.

### 3.8 Streamer Dashboard (Thiccdal.Modules.Control)

The streamer dashboard (`/dashboard`) lives in the `Thiccdal.Modules.Control` Razor Class
Library. It is a **touch surface for use while live** — no setup lives here (§3.0). It is gated
behind `<ReadinessGate>` on a saved channel plus an authorized Twitch account.

The page composes independent panels, each owning its own state and `.razor.css`:

| Panel | Role |
|---|---|
| `TopBar` | Stream status badge, platform connection indicators, go-live action |
| `PrompterPanel` | Teleprompter scroll controls (▲ / ▼) driving shared scroll state |
| `QuestionQueuePanel` | Dismiss / Feature / Complete on queued viewer questions (§3.7) |
| `LowerThirdPreviewPanel` | Shows what the lower-third overlay is currently displaying |
| `PredefinedOverlaysPanel` | Triggers registered overlay components |
| `BotCommandsPanel` | Fires bot commands without typing in chat |
| `StreamInfoPanel` | Stages title, category, and tags before going live |

#### Operator mode

`IOperatorStateService` holds an `OperatorMode` of `PreLive` or `Live`, shared across all
connected operator sessions (§3.10). Panels react to the mode rather than the page swapping
wholesale.

**Go Live** does not start the broadcast — OBS does that, independently. `IGoLiveActionService`
saves a checklist snapshot against a new session `Guid`, transitions the mode to `Live`, and
resets the checklist. Chat, the bot, and the overlay are then tracked against that session.

> **Current state:** the Pre-Live Checklist panel is not mounted on the dashboard right now, and
> **Go Live** asks for a plain confirmation rather than gating on checklist items. The checklist
> service and its persistence (§3.12) are intact and still drive personal-prep editing under
> `/config/system/checklist`. The panel layout above is under active change — treat the panel
> list as indicative, not a contract.

### 3.9 Online/Offline Status Endpoint

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
    { "name": "Twitch", "state": "Connected" }
  ]
}
```

The `platforms` array carries one entry per registered `IPlatformConnection`. `error` is present
only when that platform's state is `Error`. When offline, `"stream"` is `null`.

**`GET /status/badge.svg`** serves `badge-online.svg` or `badge-offline.svg` from `wwwroot`
depending on the current state, with caching disabled. Intended for embedding in GitHub READMEs,
websites, or stream panels without needing a downstream JSON consumer.

Both endpoints sit behind a permissive CORS policy (`StatusApi`) so external sites can read them
directly.

Alongside `/status`, `Thiccdal.API` exposes `/api/streamdeck/*` endpoints for physical Stream
Deck control — teleprompter scrolling, overlays, questions, chat, and operator mode. See
`docs/help/streamdeck-api.md`.

### 3.10 Multi-Operator Support

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

### 3.11 Platform Manual Settings Reminders

Some platform settings cannot be controlled via any API and must be configured manually in the
platform’s web dashboard before going live. These are surfaced in the Stream Info dialog as a
per-platform checklist so the operator doesn’t miss them.

The reminders are defined in code as an `IReadOnlyList<PlatformManualReminder>` returned by
`IPlatformManualReminderProvider`, and are never stored in the database — they change only when
platform capabilities change.

| Platform | Setting | Reminder text |
|---|---|---|
| Twitch | Stream encoding | "Set bitrate, resolution & keyframe interval in OBS" |
| Twitch | Stream delay | "Enable/configure stream delay in Creator Dashboard if needed" |
| Twitch | Extensions | "Activate/configure extensions in Creator Dashboard" |
| Twitch | Ad schedule | "Configure ad schedule in Creator Dashboard" |

New reminders are added by extending the list in `PlatformManualReminderProvider`
(`Thiccdal.Modules.Overlay/Services/`); no database migration is required. `Thiccdal.Remote.Null`
provides a no-op provider for tests.

### 3.12 Pre-Live Checklist

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
Every visible platform must be in `Connected` state. The system monitors
`IPlatformConnection.State` and checks/unchecks items automatically. Platforms in
`PendingApproval` or `Disabled` state produce no checklist item at all.

| Item | Type | Notes |
|---|---|---|
| Twitch connected | Auto | One item per registered `IPlatformConnection`; `Error` state blocks and surfaces `LastError` |

**📋 Stream Info** *(required)*

| Item | Type | Notes |
|---|---|---|
| Stream title set | Auto | Checks the staged pre-live title in `IOperatorStateService` is non-empty |
| Category/game set | Auto | Checks the staged pre-live category is non-empty |
| Platform manual settings reviewed | Action | Opens the Stream Info dialog; operator confirms each platform's manual reminder checklist |

**🎬 OBS & Technical** *(required)*

| Item | Type | Notes |
|---|---|---|
| OBS scene configured and active | Manual | Operator confirms the active OBS scene before going live |
| OBS connected | Auto | Present only when `Obs:Enabled` is true; driven by `IObsConnection` (§3.13) |
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
| Prompter overlay visible | Action | Operator confirms the teleprompter dock in OBS (§3.6) |
| *(additional registered overlay components)* | Action | Automatically added as new components register |

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

### 3.13 OBS Integration (Thiccdal.Remote.Obs)

OBS Studio runs on the same machine as Thiccdal and exposes **obs-websocket v5** on
`localhost:4455`. `Thiccdal.Remote.Obs` holds an authenticated session open against it and
reports what it learns through `IObsConnection` (`Thiccdal.Infrastructure/Streaming/`):

| Member | Meaning |
|---|---|
| `ObsState.IsEnabled` | The integration is switched on via `Obs:Enabled` |
| `ObsState.IsConnected` | An identified obs-websocket session is open |
| `ObsState.IsStreaming` | OBS reports an active stream output |
| `ObsState.LastError` | Why the last connection attempt failed |

`ObsConnectionHostedService` opens the session at startup and closes it at shutdown. The session
loop reconnects with exponential backoff, so OBS being closed — at startup or mid-session — is
normal state rather than an error. On connect it issues a `GetStreamStatus` request so a Thiccdal
restart mid-stream reports the truth instead of waiting for the next `StreamStateChanged` event.

The pre-live checklist consumes this: when the integration is enabled it emits a required
**OBS connected** auto item under *OBS & Technical*, surfacing `LastError` as the warning text.
When the integration is disabled the item is omitted entirely rather than sitting permanently
unchecked.

This client previously lived inside the `Thiccdal.Teleprompter.Display` desktop shell, where it
existed only to show and hide that shell's window on stream start and stop. Moving it into the
host puts the OBS connection next to the go-live workflow that cares about it, and removed the
last piece of logic justifying a separately distributed executable (see §3.6).

---

## 4. Data Model (Key Entities)

> Entity classes, `ApplicationDbContext`, and EF Core migrations all live in **`Thiccdal.Data`**.
> Interfaces and shared value types live in **`Thiccdal.Infrastructure`**.
> Platform adapters and the Blazor host reference `Thiccdal.Infrastructure`; only
> `Thiccdal.Data` references `Thiccdal.Infrastructure` for its entity implementations.

```
PlatformUser               — id, platform, platform_user_id, display_name, created_at
ChatMessage                — id, platform_user_id, platform, content, sent_at, raw_data
PlatformEvent              — id, platform, event_type (discriminator), occurred_at, raw_data
  └─ SubscribeEvent        — tier, is_gift, gifter_platform_user_id
  └─ FollowEvent           — (no extra fields beyond base)
  └─ RedeemEvent           — reward_id, reward_title, user_input
  └─ RaidEvent             — raiding_channel, viewer_count
BotCommand                 — id, trigger, response_template, handler_type (nullable), is_enabled, use_count
ProactiveMessage           — id, message, interval_seconds, is_enabled, last_sent_at
ChecklistSession           — id, session_id (Guid), recorded_at, items[]
  └─ ChecklistSessionItem  — item_id, category, label, status, is_required, warning_message
CustomChecklistItem        — id, label, sort_order, is_enabled
ChatterMemoryReset         — id, source, channel, platform_user_id, requested_by, reset_at
UserIdentity               — id, display_name, created_at, platform_users[]
UserIdentitySuggestion     — proposed links between platform users; status enum
TwitchToken                — persisted OAuth token for the Twitch adapter
TwitchTargetChannelConfiguration — target_channel, broadcaster_id, updated_at
AppConfiguration           — key/value store backing IConfigurationPersistenceService
```

Note there is no stream-session or recording entity. A live session is identified by the `Guid`
stamped onto `ChecklistSession` at go-live and held in memory by `IOperatorStateService`;
Thiccdal does not record video, so nothing tracks files on disk.

**Code-side value types** (not DB entities, defined in `Thiccdal.Infrastructure`):
- `PlatformManualReminder` — hardcoded per-platform manual setting reminders (§3.11)
- `ChecklistItemDefinition` — defines each checklist item (id, category, label, type, is_required)
- `ChecklistItemState` — runtime state per item (checked, auto-checked, blocked)
- `SystemReadiness` — which operator surfaces are usable given current configuration
- `ObsState` — OBS connection and stream-output state (§3.13)

---

## 5. Configuration Shape (IOptions)

All configuration goes through typed `IOptions<T>` classes. Never read `IConfiguration` by magic
string.

```csharp
// appsettings.json → "Twitch"
TwitchOptions
  ├── ClientId                          string
  ├── ClientSecret                      string   // user secret / env var
  ├── RedirectUri                       string
  ├── OAuthBaseAddress                  string
  ├── Scopes[]                          string
  ├── Helix                             TwitchHelixOptions
  │     ├── BaseAddress                 string
  │     ├── StreamStateRefreshSeconds   int      // default 30
  │     └── SendChatMessagesViaHelix    bool     // default true
  └── EventSub                          TwitchEventSubOptions
        ├── WebSocketUrl                string
        ├── ReconnectDelaySeconds       int      // default 5
        ├── RequireModeratorAccess      bool     // default true
        └── UseAnimatedEmotes           bool     // default true

// appsettings.json → "Obs"
ObsOptions
  ├── Enabled                        bool     // default false
  ├── Host                           string   // default "localhost"
  ├── Port                           int      // default 4455
  ├── Password                       string   // obs-websocket server password
  ├── InitialReconnectDelaySeconds   int      // default 1
  └── MaxReconnectDelaySeconds       int      // default 60

// appsettings.json → "ChatBot"
ChatBotOptions
  ├── BotName                           string   // default "Thiccdal"
  ├── AutoQueueQuestions                bool     // default true
  └── AiResponder                       ChatBotAiResponderOptions
        ├── Enabled                     bool
        ├── ChatterMemoryEnabled        bool     // default true
        ├── ChatterMemoryRetentionDays  int?     // null = no automatic cutoff
        ├── SentimentEnabled            bool     // default true
        ├── Model / MaxOutputTokenCount / Temperature / SystemPrompt

// appsettings.json → "AI:OpenAICompatible"
OpenAiOptions
  ├── Endpoint                string   // default local LM Studio endpoint
  ├── ApiKey                  string
  └── RequestTimeoutSeconds   int      // default 30

// appsettings.json → "AI:QuestionDetection"
QuestionDetectionOptions
  ├── Enabled                 bool
  └── Model / MaxOutputTokenCount / Temperature / SystemPrompt / UserPromptTemplate

// appsettings.json → "LMStudio"
LmStudioOptions
  ├── BaseAddress             string
  ├── ApiKey                  string
  └── RequestTimeoutSeconds   int      // default 30

// appsettings.json → "Prompter"
PrompterOptions
  └── ScrollStepPx            int      // default 150

// appsettings.json → "UserIdentity"
UserIdentityOptions
  └── SimilarityThreshold     double   // default 0.85

// appsettings.json → "Null"
NullOptions
  ├── PlatformName            string   // default "Null"
  └── AuthorizationUrl        string
```

### Database-backed settings

Configuration is mid-migration from `appsettings.json` toward database-backed settings. The
`AppConfiguration` key/value table plus `IConfigurationPersistenceService`
(`Thiccdal.Infrastructure/Setup/`) provide typed JSON get/set, consumed by the `/config`
surface. Most `*Options` classes still bind from `appsettings.json`.

`ISetupStateService`, `SetupStateService`, and `SetupLayout.razor` are leftovers from the removed
installation wizard — no route mounts them any more (§10).

The Twitch target channel and broadcaster ID moved to the database already
(`TwitchTargetChannelConfiguration`) and are no longer `TwitchOptions` members.

### Secrets

Platform credentials come from gitignored local `appsettings.json` overrides, user secrets, or
environment variables — increasingly from the database-backed store. Never commit a live secret
value, and never paste one into logs, commit messages, or documentation; refer to the config key
instead.

---

## 6. UI & Styling Conventions

All UI work across `Thiccdal.Modules.Control`, `Thiccdal.Modules.Overlay`,
`Thiccdal.Modules.Teleprompter`, and the `/config` pages in `Thiccdal` follows these rules:

### No third-party CSS libraries

The project uses **no third-party CSS frameworks or UI component libraries** (no Bootstrap,
Tailwind, MudBlazor, etc.). All styling is hand-authored, and no stylesheet is loaded from a CDN.

> **Known divergence:** several `/config` pages under `src/Thiccdal/Components/Config/Pages/`
> are marked up with Bootstrap class names (`card`, `btn btn-primary`, `row g-3`,
> `form-control`, `alert`, `bi bi-*` icons). Bootstrap is *not* referenced anywhere, so those
> classes resolve to almost nothing — `wwwroot/app.css` defines only `.btn-primary` and a shared
> focus rule. This is markup drift to clean up, not a hidden dependency.

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
```

---

## 7. Implementation Roadmap

Steps are numbered. **Major** headings represent cohesive phases; sub-steps are individually
shippable and testable. Items marked *(stretch)* are planned but not in the first iterations.

> **Phases 6, 7, 7b–7e, and 8 no longer exist.** They covered YouTube, Discord, LinkedIn,
> Facebook, X, and TikTok adapters plus the RTMP multicast server — all removed when the project
> narrowed to Twitch-only with video out of scope (§1). The remaining phase numbers are left as
> they were so existing step IDs stay stable; the gap is deliberate, not an omission.

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
| 10.6 | Implement Go Offline button (live mode only) | Requires confirmation; ends the Thiccdal session only — OBS stops the broadcast |
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
| 11.12 | ~~Write bUnit tests for each overlay component render~~ | Dropped — bUnit was deliberately removed; logic tests only (§9) |

---

### Phase 12 — Status Endpoint & Online/Offline Badge

| # | Step | Notes |
|---|---|---|
| 12.1 | Define `StreamStatusResponse` record | `State`, `Stream` (nullable), `Platforms[]` |
| 12.2 | Add `GET /status` returning `StreamStatusResponse` JSON | Reads from `IOperatorStateService` |
| 12.3 | Populate `stream` object from the active operator stream state | title, category, tags, startedAt, uptime |
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
| 16.6 | Implement OBS & Technical category | Manual items plus the auto **OBS connected** item from `IObsConnection` (§3.13) |
| 16.7 | Implement Overlay Verification category (action items) | Enumerates `ITestableOverlayComponent` registrations dynamically |
| 16.8 | ~~Implement Recording category (auto-with-warn)~~ | Dropped — Thiccdal does not record video (§1) |
| 16.9 | Implement Personal Prep category (manual, optional) | Loads custom items from `CustomChecklistItem` DB entity |
| 16.10 | Implement CRUD UI for custom personal prep items | Dialog; stored in DB |
| 16.11 | Implement Go Live button enabled/disabled logic | Reads `IPreLiveChecklistService.AllRequiredChecked` |
| 16.12 | Implement Go Live confirmation dialog | Shows checklist summary; lists unchecked optional items as warnings |
| 16.13 | Implement Go Live action | Saves the checklist snapshot and transitions mode to `Live`; OBS starts the broadcast independently |
| 16.14 | Add `ChecklistSession` EF entity + migration | Persists final item states per stream session |
| 16.15 | Unit-test auto-check logic for each auto item | |
| 16.16 | Unit-test Go Live button disabled until all required items checked | |
| 16.17 | Unit-test that the Go Live action saves a session and transitions mode | |

---

## 8. Agent Skills

Repo-specific conventions are packaged as skills under `.claude/skills/`. Each is a `SKILL.md`
with a `description` that states when it applies, so an agent loads it only for relevant work.

| Skill | Applies when |
|---|---|
| `platform-adapter` | Adding or changing an adapter under `src/Remote/` — registration extensions, the Infrastructure/Data seam, typed HTTP clients, connection monitors |
| `database-migrations` | Changing entity models, adding a migration, or touching database startup |
| `oauth-flow` | Implementing or changing OAuth — the mandatory CSRF state parameter, callback shape, token revocation |
| `secret-handling` | Touching configuration, platform credentials, connection strings, or the settings store |
| `nuget-cve-remediation` | Restore or build fails on an NU1901–NU1904 package advisory |
| `docs-style` | Writing or editing anything under `docs/` or `architecture/` (Microsoft Style Guide) |
| `windows-dev` | Running shell commands, scripting, or committing on the Windows dev machine |

Project-wide conventions that apply to *every* change — layering, naming, the no-`Async`-suffix
rule, testing rules — live in `CLAUDE.md` at the repo root rather than in a skill.

> An earlier revision of this document catalogued `github/awesome-copilot` skills, agents, and
> prompt templates. That tooling was removed from the repo; the templates referenced
> `.github/copilot-instructions.md`, bUnit, and per-platform adapters that no longer exist.

---

## 9. Key Non-Functional Requirements

| Concern | Decision |
|---|---|
| Latency | Chat and event display target < 500 ms end-to-end from platform to overlay |
| Resilience | Each platform adapter failure is isolated; other platforms continue unaffected |
| Security | OAuth tokens are never logged; stored in the database, user secrets, or env vars |
| Portability | Cross-platform .NET; no Windows-specific APIs without a guard |
| Offline dev | Null platform allows full UI/UX development without live credentials |
| Data retention | Chat and events are retained indefinitely by default; pruning is a future feature |
| Transport | Every surface stays reachable over plain HTTP — no HTTPS redirect, no HSTS (§3.6) |

### Testing

- Test project per source project, mirroring the source tree; a project missing from
  `Thiccdal.slnx` never runs.
- **Logic tests only — no bUnit, no `WebApplicationFactory`.** Component rendering and HTTP
  transport tests were deliberately removed; do not reintroduce those dependencies.
- Only mock external I/O (platform APIs, filesystem, clock). Never mock internal code.
- `Thiccdal.Remote.Null` is the stand-in for a live platform.

---

## 10. Open Questions & Future Considerations

- **Second platform** — The adapter architecture supports one, but adding any platform is a
  product decision that has not been made. Until it is, Twitch is the only platform (§1).
- **AI chatbot** — Free-form responses run through `Thiccdal.AI` against an OpenAI-compatible
  endpoint (LM Studio locally). Disabled by default via `ChatBot:AiResponder:Enabled`.
- **Mobile control** — Phone subset requires breakpoint design work; deprioritised after Surface Pro.
- **User identity matching** — `UserIdentity` and `UserIdentitySuggestion` are scaffolded but
  dormant; with one platform there is nothing to correlate across (see Phase 13).
- **Authentication** — Explicitly out of scope for v1. All operator sessions are trusted.
- **Clip creation** — Triggering Twitch clip creation from the operator UI is a future feature.
- **Moderation actions** — Ban/timeout from the operator UI is a future feature.
- **Settings migration** — Configuration is mid-move from `appsettings.json` to the
  `AppConfiguration` store. Most `*Options` classes still bind from JSON (§5).
- **`IStreamTarget`** — An empty marker interface left over from the removed restreaming
  feature. Nothing implements behaviour for it; a candidate for deletion (§3.1).
- **Setup wizard remnants** — `ISetupStateService`, `SetupStateService`, `SetupState`, and
  `SetupLayout.razor` survive from the wizard that `/config` replaced. No route reaches them;
  another candidate for deletion.
- **Command token extensibility** — New interpolation tokens (e.g. `{title}`, `{game}`) can be
  added to the token resolver without changing the DB schema.

---

*Last reviewed against the codebase: 2026-08-24.*
