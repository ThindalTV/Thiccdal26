# Roadmap to 1.0

> Snapshot taken on 4 October 2026 against `main` at `0055cdb` plus the uncommitted two-account
> Twitch work. Line numbers refer to that state. Update this page as milestones close.

Most of what 1.0 needs is already built and tested. What's missing is what makes it safe to rely
on during a live show: staying connected to Twitch, knowing when you're live, config pages that
save what they show, and a way to install it on the stream PC and reach it from the tablet.

This plan assumes 1.0 means **dependable for your own streams on your own stream PC**. If 1.0
should be something other streamers can install, see decision D1 first.

Work is tracked in the [v1.0 milestone](https://github.com/ThindalTV/Thiccdal26/milestone/1) on
GitHub. Each task below links to its issue; close the issue and tick the box together.

## Status at a glance

| Item | State |
|---|---|
| Build | Clean: 0 warnings, 0 errors, warnings-as-errors on |
| Tests | 400 of 400 pass across 7 test projects, uncommitted work included |
| CI on `main` | Failing since 22 Aug: the Docker job fails every run; tests pass in the latest run |
| Working tree | 48 uncommitted files (+2,721 / −1,829 lines) from 24–25 Aug |
| Releases | None: no version number, tag, or package |
| Open issues | 17 at the snapshot; 13 closed in triage on 4 Oct, 30 roadmap issues added (#200–#229) |
| Packages | No known vulnerable packages, direct or transitive |
| SDK | No `global.json`, so local builds use whatever SDK is newest (currently .NET 11 RC) |

### Uncommitted work: two Twitch accounts

The working tree adds a bot account (reads and sends chat) and a broadcaster account (follows,
subs, cheers, redemptions, title, and category). It builds and every test passes.

- `TwitchTokenRole` plus a `Role` column on `TwitchTokens` (migration `AddTwitchTokenRole`).
- Token manager, OAuth state, and callback work per role; `Twitch:Scopes` splits into
  `BotScopes` and `BroadcasterScopes`, which adds `channel:manage:broadcast`.
- One EventSub WebSocket session per authorized account, and `RefreshSubscriptions` after a new
  authorization.
- `/config/system/twitch` shows both accounts.

Loose ends before you commit it:

- `TwitchService.Connect`, both stream-state pollers, and dashboard readiness still require the
  bot token, though EventSub can run on the broadcaster alone (D8).
- `TwitchSetupDialog.razor` accounts for about 1,000 changed lines, but nothing renders it.
- Existing tokens migrate as the bot role, so you need to authorize the broadcaster once after
  upgrading.
- `help/connecting-to-twitch.md` still describes an IRC step and an Integrations page.

### CI, branches, and tags

- `src/Thiccdal/Dockerfile` builds on `sdk:10.0-preview`, which can't compile the C# 14
  `extension` blocks in five registration files.
- The README badge points at `ci.yml`; the workflow is `build.yml`.
- All 13 remote branches are merged. Only the local `feature/9-TwitchConnection` (RTMP-era work)
  and three `backup/*` tags remain unmerged.

## What exists today

| Area | What's there | Status | Gap for 1.0 |
|---|---|---|---|
| Twitch connection | OAuth with CSRF state, token refresh, EventSub, Helix chat send, emotes | Partial | No reconnect (C1); startup tied to Twitch (C2) |
| Platform events | Chat, follow, sub, resub, gift sub, cheer, raid, redemption; saved before dispatch | Works | No `stream.online` or `stream.offline` (C3) |
| Bot commands | Database CRUD, `{user}` `{platform}` `{count}` `{uptime}`, handlers, effects | Works | `{uptime}` reads unreliable live state (C3) |
| Timed messages | Database-backed autoresponses | Partial | Post while offline (H5) |
| Greetings | Form for stream-start, follow, and sub messages | Mock | Doesn't save; no greeting logic (H2) |
| Question queue | Manual, command, or AI detection; feature, dismiss, complete; synced | Works | None |
| Lower third and overlay cards | One owner service, cards in the database | Works | None |
| Overlay `/overlay` | Chat feed, event ticker, lower third, sponsor badge, test flash | Works | One combined page (after 1.0) |
| Teleprompter `/prompter` | Chat and event feed, flashes, remote scroll, sponsor read | Partial | No script (H7, D3) |
| Dashboard `/dashboard` | Three columns, Stream Info dialog, viewer count, badges | Partial | Badges can be wrong (C1, C3) |
| Pre-live checklist and Go Live | Services, persistence, item editing | Partial | No page runs it; Go Live only via Stream Deck (D2) |
| Stream info | Title, category, tags pushed to Twitch | Works | Doesn't load current values (H6) |
| `/config` | 11 pages under Bot and System | Partial | 2 mockups, 2 settings lost on restart (H1–H3) |
| AI | Question detection, replies, chatter memory | Partial | Off by default; `appsettings.json` only (H1, D4) |
| OBS | obs-websocket v5, read-only stream state | Works | Not used to detect live (C3) |
| APIs | `/status`, badge, `/api/streamdeck/*`, `/health`, `/ready` | Works | No access control (P7); `GET /status` changes state (C3) |
| Running it | `dotnet run` profiles, Aspire AppHost | Missing | LAN binding, data folder, packaging (P1–P4) |

## What blocks 1.0

Paths are relative to `src/`.

### Critical: would break during a live stream

**C1. Twitch doesn't reconnect after a drop.** `TwitchEventSubClient` raises `Disconnected` and
`Faulted`, and nothing subscribes. `EventSub:ReconnectDelaySeconds` is validated but never used,
and `TwitchService.State` stays Connected. Chat, commands, and events stop silently. There's also
no keepalive watchdog.
Evidence: `Remote/Thiccdal.Remote.Twitch/TwitchEventSubClient.cs:282–297`, `TwitchService.cs:24, 34`.

**C2. Startup and the first connection depend on timing.** `ChatAggregationService.StartAsync`
calls `TwitchService.Connect`, which rethrows. If Twitch is unreachable at boot with an account
authorized, the host doesn't start (from reading the code, not reproduced). After the first
authorization, nothing connects chat; the Connect button in `/config/system/twitch` is the only way.
Evidence: `Modules/Thiccdal.Modules.ChatBot/Services/ChatAggregationService.cs:53–98`,
`Remote/Thiccdal.Remote.Twitch/TwitchService.cs:103–153`.

**C3. Nothing owns "are we live".** Live and Pre-Live only switch as a side effect of
`GET /status`, a Stream Deck call, or the go-live action. The top bar's viewer count comes from a
separate poller, and `{uptime}` reads the same unreliable state.
Evidence: `Thiccdal.API/Status/StreamStatusService.cs:38–120`,
`Modules/Thiccdal.Modules.ChatBot/Services/CommandDispatcher.cs:310`.

### High: shows one thing, does another

- **H1. AI keys page is a mockup.** It loads and saves nothing, and **Test Connection** waits one
  second and reports success (`Thiccdal/Components/Config/Sections/AiSettingsSection.razor:124–151`).
- **H2. Identity and greetings page is a mockup.** Fields aren't saved, and the bot has no
  greeting logic (`BotIdentitySection.razor`).
- **H3. Two settings reset on restart:** sponsorship (`SponsorshipService` memory) and the
  animated-emotes switch on **Appearance**.
- **H4. Twitch authorization lands on the dashboard.** The callback redirects to
  `/dashboard?twitch_error=…`, which nothing reads.
- **H5. Timed messages post while offline** (`ProactiveMessagingService.cs:90`).
- **H6. Stream Info doesn't load the channel's current title and category** (#188).
- **H7. The teleprompter has no script**, though the README and `CLAUDE.md` describe one.

### Deploy: needed to run it on the stream PC

These describe the committed defaults; local overrides weren't checked.

- **P1. The tablet can't reach it by default.** Nothing binds Kestrel to the network.
  `help/getting-started.md` suggests port 5082; the launch profile uses 5226.
- **P2. The OAuth redirect only works in development.** `Twitch:RedirectUri` points at the HTTPS
  dev profile. Twitch accepts `http://localhost`, so authorize from the stream PC.
- **P3. The database follows the working folder.** `Data Source=thiccdal.db` is relative, so
  starting from another folder creates an empty database.
- **P4. No way to install or start it day to day.** No publish profile, service, version, or
  release. The README run path doesn't exist.
- **P5. The docs put the client secret in a tracked file.** Use user secrets or environment
  variables instead.
- **P6. Tokens are stored in plain text**, while the help page suggests encryption.
- **P7. Nothing is access-controlled.** `POST /api/streamdeck/chat/send` lets any device on the
  network post as the bot (D6).
- **P8. The Docker image doesn't match how Thiccdal runs** next to OBS on Windows (D7).

## Decisions to make first

Tracked in #200.

| ID | Question | Recommendation |
|---|---|---|
| D1 | What does 1.0 mean? | Your own stream PC. Make distribution the theme of 2.0. |
| D2 | Keep the pre-live checklist and Go Live? | Drop manual Go Live, detect live automatically (M2), close #97 and #98. |
| D3 | Does the teleprompter need a script? | Ship without, fix the README wording. Add to M3 (2–3 days) if you read a run of show on camera. |
| D4 | What happens to the AI settings page? | Keep AI off and experimental; make the page read-only with a real connection test. |
| D5 | Build greetings or hide them? | Build follow, sub, and raid thank-yous (about 2 days), or hide the page. |
| D6 | How much LAN security? | No login; private network profile only; optional shared key on `/api/streamdeck/*`. |
| D7 | Keep the Docker image? | Remove the job and publish a Windows build. |
| D8 | Support broadcaster-only Twitch setups? | Require the bot account for 1.0 and say so on the Twitch page. |

## Milestones

| Milestone | Size | Focused days |
|---|---|---|
| M0: Land the WIP and get CI green | S | 1–2 |
| M1: Stream-safe Twitch connection | M | 3–5 |
| M2: One owner for live state | S–M | 2–3 |
| M3: Configuration that saves | M | 2–3 must, +2–3 should |
| M4: Run it on the stream PC | M | 2–4 |
| M5: Rehearsal and release | S | 1–2 plus one stream |
| Cleanup, anytime | S | 1–2 |
| **Total** | | **11–19 must, 14–24 all** |

### M0: Land the work in progress and get CI green

- [ ] Decide D1–D8 (#200).
- [ ] Finish and commit the two-account Twitch work: D8 loose ends, delete `TwitchSetupDialog`, fix or remove the `/twitch/connect` shim, finish `help/connecting-to-twitch.md`, merge through a PR (#201).
- [ ] Add a `global.json` that pins the .NET 10 SDK (#202).
- [ ] Fix CI: remove or repair the Docker job (D7), fix the README badge and run command, move actions off Node 20 (#203).
- [ ] Remove the stale `NU1902`/`NU1903` and advisory suppressions from `Directory.Build.props` (#204).
- [x] Triage issues and create the v1.0 milestone (done 4 Oct; see below).
- [ ] Delete merged remote branches (#229).

**Done when** CI is green on `main`, the working tree is clean, and open issues match this plan.

### M1: Stream-safe Twitch connection

- [ ] Add a supervisor that handles `Disconnected` and `Faulted`, reconnects each session with backoff, and resubscribes (#168).
- [ ] Add a keepalive watchdog per session based on `keepalive_timeout_seconds` (#205).
- [ ] Report the real state on badges and the Twitch page (#208), including "re-authorize needed" when a scope is missing (#169).
- [ ] Let the host start while Twitch is unreachable and retry in the background (#206).
- [ ] Connect automatically once a channel is saved and an account is authorized, including after the OAuth callback (#207).
- [ ] Each issue includes logic tests for its case.

**Done when** pulling the network cable for a minute mid-stream recovers on its own, and Thiccdal
starts with the network down.

### M2: One owner for live state

- [ ] Move Live, Pre-Live, and the start time into one hosted service; make `GET /status` read-only; merge the two Helix pollers; point `{uptime}`, the status badge, and the Stream Deck mode endpoints at it (#209).
- [ ] Subscribe to `stream.online` and `stream.offline`; keep a Helix poll as fallback; use OBS's streaming flag when enabled (#210).
- [ ] Pause timed messages while offline, with an opt-out setting (#211).
- [ ] Apply D2: remove the go-live leftovers (#212). #97 and #98 were closed as superseded on 4 Oct.

**Done when** the badge, `/status`, and an `{uptime}` command agree within 30 seconds of OBS
starting or stopping the stream.

### M3: Configuration that saves what it shows

Must:

- [ ] AI keys: read-only view of active settings and a real connection test (D4, #213).
- [ ] Save sponsorship and appearance through `IConfigurationPersistenceService` (#214).
- [ ] Return the OAuth callback to `/config/system/twitch` and show its errors (#215).
- [ ] Load the current title and category into Stream Info (#188).

Should:

- [ ] Greetings: save templates and send follow, sub, and raid thank-yous (D5), or hide the page (#216).
- [ ] Replace Bootstrap class names on touched pages with module CSS (#217).
- [ ] Teleprompter script support, if D3 says yes (#218).

**Done when** restarting Thiccdal changes nothing you set in `/config`.

### M4: Run it on the stream PC

- [ ] Set a fixed port and LAN binding, add a private-profile firewall rule, and document the tablet URL (#219).
- [ ] Use a stable data folder (for example `%LOCALAPPDATA%\Thiccdal`) and back up the database before migrations (#220).
- [ ] Use a production redirect on `http://localhost:<port>/auth/twitch/callback` and register it in the Twitch console (#221).
- [ ] Publish a self-contained `win-x64` build that starts at sign-in or as a Windows service, without Aspire (#222).
- [ ] Add the optional shared key on the Stream Deck API (D6, #223).
- [ ] Set version 1.0.0, start a CHANGELOG, and add a tag-triggered release job (#224).
- [ ] Rewrite `help/getting-started.md` and the README quick start; state that tokens are unencrypted (#225).

**Done when** you can reinstall from the release zip and the docs alone, and control it from the
tablet.

### M5: Rehearsal and release

All tracked in #226.

- [ ] Rehearse with a second Twitch account as the broadcaster.
- [ ] Walk a run sheet: every dashboard panel from the tablet, the prompter dock, the overlay source, commands and effects, a question to the lower third, Stream Deck buttons, two synced tabs (#144).
- [ ] Run fault drills: drop the network for a minute, restart mid-stream, stay live past four hours.
- [ ] Fix what breaks, then tag v1.0.0.

**Done when** a real stream runs end to end and nobody opens `/config` during it.

## Cleanup track

**Dead code** (#227)

- [ ] `TwitchSetupDialog.razor` and the `/twitch/connect` shim, which redirects to the deleted `/integrations` page.
- [ ] Setup wizard leftovers: `PlatformStatusButton`, `SetupLayout`, `SetupState`, `ISetupStateService`, `SetupStateService` and its tests.
- [ ] `IStreamTarget`, the empty restreaming marker.
- [ ] `Thiccdal.Remote.LMStudio` and `Infrastructure/LmStudio/*`; only tests use them.
- [ ] YouTube and Facebook branches in `PlatformUserIdResolver`.
- [ ] `/input-library`, `plan.md`, `docker-compose.rtmp.yml`, and the local `src/Thiccdal.Restreamer/` folder.

**Docs drift** (#228)

- [ ] README: badge, run command, "all connected platforms".
- [ ] `CLAUDE.md`: no `architecture/` folder exists; it still mentions a setup wizard.
- [ ] `architecture/overview.md` §7 lists pre-refactor phases; point it at this page.
- [ ] `help/pre-live-workflow.md` describes the removed pre-live dashboard.
- [ ] `help/connecting-to-twitch.md` links to three pages that don't exist.
- [ ] `help/chatbot-settings.md` only covers `appsettings.json`.
- [ ] The `secret-handling` skill still lists Discord, YouTube, Facebook, and X tokens.

**Repository** (#229)

- [ ] Delete the 13 merged remote branches.
- [ ] Archive or delete `feature/9-TwitchConnection` and the three `backup/*` tags.
- [ ] Remove the `squad:*` labels.

## Issue triage

Done on 4 October 2026. The 17 issues open at the snapshot were checked against the code; 13
were closed with a comment explaining why, and the other 4 moved to the v1.0 milestone.

| # | Issue | What the code shows | Status |
|---|---|---|---|
| 188 | Load title and category from Helix | Not done | Open, v1.0 (M3) |
| 169 | Scopes and scope-upgrade re-auth | Scopes done; no missing-scope detection | Open, v1.0 (M1) |
| 168 | EventSub manager with reconnect | Reconnect missing | Open, v1.0 (M1) |
| 144 | Manual two-tab sync test | Not run | Open, v1.0 (M5) |

If D2 goes the other way and you keep the Go Live button, reopen #97 and #98.

## After 1.0

- Teleprompter script and run of show, unless D3 pulls it in.
- One route per overlay element, so each OBS source shows only its element.
- A pre-show checklist page.
- Editable AI settings and finishing the move to database-backed settings.
- Retention for chat and events.
- Token encryption at rest.
- Login, in-app Twitch credentials, and an installer, if D1 points at other streamers.
- Clip creation and moderation actions.
- A phone layout.

## Related

- [Architecture overview](architecture/overview.md)
- [Getting started](help/getting-started.md)
- [Connecting to Twitch](help/connecting-to-twitch.md)
