# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Inara owns the Blazor operator experience, UX polish, and UI ergonomics.

## Recent Updates

📌 Firefly squad configured on 2026-05-27

## Learnings

- The operator UI is tablet-oriented and must stay touch-friendly.
- The control, overlay, and teleprompter modules are separate UI surfaces.

### 2026-05-27: Helix API + Emote/Event Support Analysis

**Discovery:**
- Current Twitch integration uses IRC protocol (deprecated) with manual PRIVMSG parsing
- ChatEvent model lacks emote, badge, and color metadata — Helix EventSub provides all three
- Teleprompter `ChatLine.razor` and Overlay `ChatView.razor` render plain text only
- UI changes span both modules and must handle safe image rendering + fallbacks

**Key Constraints:**
- Emote URLs must be whitelisted (`static-cdn.jtvnw.net`) to prevent XSS
- Emote images must be touchable on 7-10" tablets (min 20px)
- Event type filtering needed to prevent UI clutter (follows, subs, raids, bits, redemptions)
- Emote CDN failover required (fallback to `[emote_name]` text)

**Data Model Gaps:**
- Need `TwitchEmote` (id, name, URL, position in message)
- Need `TwitchBadge` (type, version)
- Need typed event classes: `TwitchFollowEvent`, `TwitchSubscribeEvent`, `TwitchRaidEvent`, etc.
- ChatEvent must extend to hold emote/badge lists + user color

**File Paths (Current State):**
- Infrastructure contracts: `src/Thiccdal.Infrastructure/Bot/Models/`
- Teleprompter: `src/Modules/Thiccdal.Modules.Teleprompter/Components/ChatLine.razor*`
- Overlay: `src/Modules/Thiccdal.Modules.Overlay/Components/ChatView.razor*`
- Twitch adapter: `src/Remote/Thiccdal.Remote.Twitch/TwitchService.cs`

**Sequencing:** Infrastructure (Kaylee) → Helix integration (River) → UI rendering (Inara). Parallelizable: CSS + test fixtures.

**Decision Logged:** `.squad/decisions/inbox/inara-prompter-impact.md` with full impact analysis, UX risks, and GitHub issue routing guide.

### 2026-05-28: Helix Redesign Phase 18 Scope — Inara Lead on Emote/Badge Rendering

**Team update from Mal (Lead):**
- Helix EventSub architecture locked (see `docs/architecture/helix-redesign.md`).
- ChatFragment hierarchy ready: TextFragment, EmoteFragment, CheermoteFragment, BadgeFragment.
- ChatEvent extended with Fragments list, Color field, Badges list.

**Inara's Phase 18 ownership (UI rendering, 4 issues directly assigned):**
- #176: Extend Line model with Fragments (Teleprompter backend model)
- #177: Render emote fragments in PrompterLine.razor (Teleprompter emote images + fallback)
- #178: Render emote fragments in ChatFeedOverlayComponent (Overlay emote images + fallback)

**Inara's Phase 19 ownership (Rich events, 1 issue directly assigned):**
- #185: Wire significant events to prompter gold flash (Teleprompter event-driven effects)

**Inara's involvement (mixed ownership):**
- #187: Wire Helix SetStreamInfo to operator UI (Phase 20, Helix metadata in operator UI)

**Security handling (XSS prevention):**
- Emote URLs whitelisted to `static-cdn.jtvnw.net` (deterministic CDN).
- Use Blazor `MarkupString` safely or render via safe HTML builder.
- No user-provided HTML in message rendering.
- Test with mock Helix responses before live Twitch data.

**Touch-friendly constraints:**
- Emote min size: 20–24px (readable on 7-10" tablet screens).
- Badge icons: ~16px.
- No hover-only interactions; all touches must be click-safe.
- Test on tablet form factor early (pinch-zoom support optional).

**Emote rendering phases (per architecture doc):**
- Phase 1 (MVP): Plain-text message + emote fallback (`[emote_name]`)
- Phase 2: Emote image rendering + fallback (CDN failover testing)
- Phase 3: Badge rendering + user color styling
- Phase 4: Event type rendering (Follow/Sub/Raid/Bits/Redemption layouts)

**CSS updates required:**
- `.emote-inline` — inline emote image styling
- `.emote-fallback` — text-based emote fallback
- `.badge-icon` — small badge styling
- `.author-name` — apply user color
- `.event-follow`, `.event-sub`, `.event-raid`, etc. — event-type colors/icons
- `.chat-message-rich` — message with emotes/badges
- `.chat-event` — system event rendering

**GitHub labeling:** All Inara's issues routed with `squad:inara` label (48 issues total). Phase 18–19 issues in queue for Phase 17 completion.
