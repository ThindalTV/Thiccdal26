# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

River handles platform adapters, integration seams, and external API contracts.

## Recent Updates

📌 Firefly squad configured on 2026-05-27

## Learnings

- Platform adapters implement shared infrastructure contracts and feed typed events into the system.
- Twitch work belongs with River unless it is purely UI or backend service wiring.
- Current Twitch code lives in `src\Remote\Thiccdal.Remote.Twitch\` and is still a minimal IRC adapter: `TwitchService` only parses `PRIVMSG`, while `TwitchTokenManager` owns token exchange/refresh against Twitch OAuth.
- The architecture target in `docs\architecture\overview.md` expects EventSub-driven typed platform events, persisted chat/event records, and a combined chat+event prompter feed; the current code does not yet provide that seam.
- The prompter path is currently `Modules.ChatBot` -> `IChatService` -> `Modules.Teleprompter\Pages\Prompter.razor`, and `ChatLine.razor` renders plain text only, so emotes/events need normalized fragments before they can reach the streamer-facing view cleanly.
- GitHub Phase 5 Twitch issues (#24-#31) are still useful for routing, but their implementation assumptions are IRC/TwitchLib-centric and should be re-scoped toward Helix + EventSub, with chat work labeled through `area/chatbot` and downstream presentation work routed separately through teleprompter/overlay labels.

### 2026-05-28: Helix EventSub Architecture Locked — River Lead on Phase 17

**Team update from Mal (Lead):**
- Helix EventSub redesign is locked for implementation (see `docs/architecture/helix-redesign.md`).
- Pure EventSub WebSocket (not IRC + EventSub hybrid).
- New data model: ChatFragment hierarchy (TextFragment, EmoteFragment, CheermoteFragment, CheermoteFragment), extended ChatEvent with Fragments/Color/Badges, typed PlatformEvent subtypes.
- 6+ new OAuth scopes required; startup must validate and prompt for re-auth.
- Emote CDN: deterministic Twitch URLs (no HTTP lookup).
- Inline OAuth flow: operator login on first startup, token persisted in SQLite.

**River's Phase 17 ownership (6 issues):**
- #166: Expand TwitchOptions (BroadcasterId, new scopes, EventSub config)
- #167: Implement TwitchHelixClient (typed HTTP client for Helix REST)
- #168: Implement EventSub WebSocket manager (connect, subscribe, reconnect)
- #169: Update OAuth scopes + scope-upgrade re-auth flow
- #170: Define ITwitchEventSubClient in Infrastructure
- #171: Update Thiccdal.Remote.Twitch.Tests scope

**Sequencing:** Phase 17 foundation must complete before Phase 18 (EventSub client is transport). Phase 20 (Helix stream info) can begin in parallel with Phase 18/19.

**GitHub labeling:** All 152 open issues now routed to squad members. Old Phase 5 issues #24–31 closed as superseded.

**Open questions for ThindalTV:**
- Cheer bits threshold for gold flash (suggested: 100)
- Bot mod status in broadcaster channel (affects `moderator:read:followers` scope)
- Animated vs static emotes preference
