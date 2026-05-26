# Twitch Helix EventSub Redesign

> **Status:** Architecture Decision  
> **Phase:** 17–20 (Helix/EventSub Foundation & Emote Rendering & Event Coverage & Stream Info)  
> **Decision Date:** 2026-05-28  
> **Audience:** Developers, architects, reviewers  

---

## 1. Motivation

The current Thiccdal Twitch integration uses **raw TCP IRC** (port 6667, no TLS) for chat ingestion. This approach has significant limitations:

- **Security**: No encryption, no CAP REQ for message tags, plain-text authentication
- **Feature Gap**: No emote data, no badge data, no subscriber/cheerer context
- **API Compliance**: IRC is legacy; Twitch actively promotes EventSub for all new integrations
- **Operational**: Manual reconnection logic, no built-in resiliency from official APIs
- **Telemetry**: No access to structured metadata (emote IDs, badge versions, cheermote tiers)

This decision replaces IRC with **pure EventSub WebSocket** — Twitch's official, encrypted, WebSocket-based event delivery system — enabling full emote rendering, subscriber/cheerer awareness, and future extensibility.

---

## 2. Current State Problems

### 2.1 Data Model Gaps

- `ChatEvent.Content` is plain text only; no structured fragments (text, emotes, badges, cheermotes)
- `ApplicationDbContext` has only `TwitchToken`; no `ChatMessage`, `PlatformUser`, or `PlatformEvent` entities yet (issues #10, #11, #14)
- Emote information is unavailable; rendering cannot distinguish text from emotes in chat output
- Badge data (subscriber, founder, moderator) not persisted or displayed

### 2.2 Integration Approach

- `TwitchService` connects via raw TCP to irc.chat.twitch.tv:6667 (no TLS)
- No EventSub subscriptions; events are unstructured IRC PRIVMSG and USERNOTICE lines
- `TwitchTokenManager` handles OAuth2 correctly (refresh, storage), but scopes are limited to `chat:read chat:edit`
- `ITwitchService` interface is an empty placeholder; no contract defined

### 2.3 UI Rendering

- Teleprompter `Line.HtmlContent` field exists but is unused
- Overlay has no structured emote-rendering capability
- Chat display is plain text; no visual distinction for badges, emotes, or subscriber status

### 2.4 Authentication

- Credentials currently in `appsettings.json` or environment variables (config-based)
- No user-facing OAuth flow; operator must supply credentials manually

---

## 3. Architectural Decision

### 3.1 Use Pure EventSub WebSocket

Replace raw TCP IRC with **EventSub WebSocket** as the single, official Twitch event channel:

- **Protocol**: WebSocket (TLS 1.2+, automatic reconnection, built-in heartbeat)
- **Events**: Subscribe to `channel.chat.message` (replaces IRC PRIVMSG)
- **Metadata**: EventSub delivers emote, badge, and cheermote data inline with each message
- **Authentication**: OAuth2 via `user:read:chat` + `chat:read:chat:edit` scopes (bot account, broadcasterRead mode for follow events with `moderator:read:followers`)

### 3.2 Chat Fragment Hierarchy

Introduce a **ChatFragment** inheritance hierarchy to represent structured chat content:

```csharp
public abstract record ChatFragment;

public record TextFragment(string Content) : ChatFragment;

public record EmoteFragment(string EmoteId, string Name, string Url) : ChatFragment;

public record CheermoteFragment(string Name, int Amount, string Tier, string Url) : ChatFragment;

public record BadgeFragment(string Type, string Version, string Url) : ChatFragment;
```

Each `ChatEvent` will carry `ChatFragment[]` in addition to the plain-text `Content` fallback for backward compatibility.

### 3.3 Emote CDN Strategy

Twitch emote CDN URLs are deterministic and require no HTTP call:

```
https://static-cdn.jtvnw.net/emoticons/v2/{emoteId}/{default|animated}/dark/1.0
```

- **Resolution**: Fetch at event time; emote IDs are immutable
- **Caching**: Optional in-memory LRU cache for frequently used emotes
- **Format**: Configurable (static vs. animated) via `IOptions<TwitchOptions>`

---

## 4. Authentication Approach: Inline OAuth

**User Directive**: Use inline Twitch authentication instead of config-based credentials.

### 4.1 Flow

1. **First Run**: Operator opens Thiccdal control UI → detects no stored token → displays "Login to Twitch" button
2. **OAuth Window**: Click opens a Twitch login page in a browser window (OAuth code grant)
3. **Token Exchange**: `TwitchTokenManager` exchanges code for access token + refresh token
4. **Persistence**: Token stored in SQLite (`TwitchToken` entity), encrypted if necessary
5. **Refresh**: On startup or expiration, `TwitchTokenManager` automatically refreshes
6. **Logout**: Operator can revoke token from Control UI; system reverts to login prompt

### 4.2 Required Scopes

```
user:read:chat              # Read chat messages from the authenticated user's channel
chat:read:chat:edit         # Read/send chat messages in the authenticated user's channel (bot scope)
moderator:read:followers    # (Conditional) Read follower events if bot is channel moderator
```

**Open Question**: Does the bot user have moderator status in the broadcaster's channel? This determines whether `moderator:read:followers` can be used; otherwise, follow events via EventSub are unavailable and fallback to IRC or polling is needed.

---

## 5. Impacted Projects

### 5.1 Core Changes

| Project | Change | Reason |
|---------|--------|--------|
| `Thiccdal.Remote.Twitch` | Full rewrite of `TwitchService`; add `EventSubClient` | Replace IRC with EventSub |
| `Thiccdal.Data` | Add `ChatMessage`, `PlatformUser`, `PlatformEvent`, `ChatFragment*` entities | Persist structured chat data |
| `Thiccdal.Infrastructure` | Enhance `IChatService` contract; add `ChatFragment` hierarchy | Define EventSub-aware interface |
| `Thiccdal.Modules.Teleprompter` | Use `ChatFragment[]` to render emotes and badges | Visual emote/badge display |
| `Thiccdal.Modules.Overlay` | SignalR -> HTML rendering for emotes, badges, cheermotes | Overlay event display |
| `TwitchTokenManager` | Add inline OAuth UI hooks; refactor to support token refresh | Interactive auth flow |

### 5.2 Minimal Impact

| Project | Status |
|---------|--------|
| `Thiccdal.Modules.Control` | UI enhancements only (login button, token revocation) |
| `Thiccdal.Streaming` | No changes (RTMP relay is independent) |
| `Thiccdal.API` | No changes (status endpoint unaffected) |
| Other Remote adapters | No changes |

---

## 6. Data Model Changes

### 6.1 New Entities

#### PlatformEvent

Replaces ad-hoc event tracking; persists all platform events (IRC, EventSub, YouTube, Discord, etc.).

```csharp
public class PlatformEvent
{
    public long Id { get; set; }
    public PlatformEventSource Source { get; set; }  // Twitch, YouTube, Discord, etc.
    public PlatformEventType Type { get; set; }      // ChatMessage, Follow, Subscribe, etc.
    public string RawData { get; set; }              // Full JSON / raw message for debugging
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }  // Structured fields (emote IDs, badge info, etc.)
}

public enum PlatformEventSource { Twitch = 1, YouTube = 2, Facebook = 3, X = 4, Discord = 5, Null = 99 }
public enum PlatformEventType { ChatMessage, Follow, Subscribe, Redeem, Cheer, … }
```

#### ChatMessage

Structured chat data, persisted for searchability and replay.

```csharp
public class ChatMessage
{
    public long Id { get; set; }
    public long PlatformEventId { get; set; }  // FK to PlatformEvent
    public PlatformEventSource Source { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string Content { get; set; }  // Plain text (backward compat)
    public ChatFragment[] Fragments { get; set; } = [];  // Structured (text, emotes, badges, cheermotes)
    public UserBadge[] Badges { get; set; } = [];
    public int? CheermoteAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record UserBadge(string Type, string Version);
```

#### ChatFragment (Entity Base)

Abstract base for fragment types; supports JSON serialization.

```csharp
public abstract class ChatFragment
{
    public int Order { get; set; }  // Position in message
    public string FragmentType { get; set; }  // "text", "emote", "cheermote", "badge"
}

public class TextFragment : ChatFragment { public string Content { get; set; } }
public class EmoteFragment : ChatFragment { public string EmoteId { get; set; } public string Name { get; set; } }
public class CheermoteFragment : ChatFragment { public string Name { get; set; } public int Amount { get; set; } public string Tier { get; set; } }
```

#### PlatformUser

Track user metadata (follower status, subscribe status, mod status).

```csharp
public class PlatformUser
{
    public long Id { get; set; }
    public PlatformEventSource Source { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsFollower { get; set; }
    public bool IsSubscriber { get; set; }
    public int? SubscriptionMonths { get; set; }
    public bool IsModerator { get; set; }
    public DateTime LastSeen { get; set; }
}
```

### 6.2 EventSub Event Mapping

Map Twitch EventSub events to Thiccdal `PlatformEvent` types:

| EventSub Event | PlatformEventType | Notes |
|---|---|---|
| `channel.chat.message` | ChatMessage | Main chat ingestion |
| `channel.follow` | Follow | Requires `moderator:read:followers` scope or bot mod status |
| `channel.subscribe` | Subscribe | V5 subscription event |
| `channel.cheer` | Cheer | Bits/cheermotes |
| `channel.redeem` | Redeem | Channel points redemption |
| (future) `stream.online` | StreamOnline | Stream state tracking |
| (future) `stream.offline` | StreamOffline | Stream state tracking |

---

## 7. Phased Implementation Plan

### Phase 17: Helix/EventSub Foundation (6 issues)

- [ ] Create `EventSubClient` in `Thiccdal.Remote.Twitch` (WebSocket connection, subscription management)
- [ ] Add `PlatformEvent`, `ChatMessage`, `PlatformUser` entities + migration
- [ ] Implement `channel.chat.message` EventSub subscription handler
- [ ] Parse plain-text chat from EventSub event (backward-compat; no fragment parsing yet)
- [ ] Refactor `TwitchTokenManager` to support inline OAuth flow + token refresh
- [ ] Write integration tests for EventSub client reconnection and event handling

**Deliverable**: EventSub WebSocket receives `channel.chat.message` events; plain text stored in `ChatMessage.Content`.

### Phase 18: ChatFragment Hierarchy + Emote Rendering (8 issues)

- [ ] Add `ChatFragment` base class and derived types (TextFragment, EmoteFragment, CheermoteFragment, BadgeFragment)
- [ ] Update `ChatMessage` to carry `ChatFragment[]`
- [ ] Parse EventSub message data into fragments (emote positions, badge data, cheermote data)
- [ ] Implement emote CDN URL builder + in-memory LRU cache
- [ ] Update Teleprompter `Line` rendering to display emotes from fragments
- [ ] Update Overlay to render `ChatFragment[]` with emotes, badges, cheermotes
- [ ] Add Teleprompter config option: static vs. animated emotes
- [ ] Write unit tests for fragment parsing and rendering

**Deliverable**: Teleprompter and Overlay display emotes, badges, and cheermotes from chat.

### Phase 19: Full Event Coverage + IEventBus (6 issues)

- [ ] Extend `EventSubClient` to subscribe to `channel.follow`, `channel.subscribe`, `channel.cheer`, `channel.redeem`
- [ ] Map EventSub events to `PlatformEventType`; persist raw + metadata
- [ ] Implement `IEventBus` for in-app event dispatch (PlatformEvent → subscribers)
- [ ] Wire EventSub events to event bus subscribers (Overlay, Teleprompter, ChatBot listeners)
- [ ] Add "gold flash" effect to Overlay on cheer/redeem (configurable threshold)
- [ ] Write integration tests for multi-event scenarios

**Deliverable**: Overlay and Teleprompter react to follows, subs, redeems, and cheers via event bus.

**Open Question**: What bits threshold triggers the "gold flash" overlay effect? (e.g., 100 bits = flash, <100 bits = no flash)

### Phase 20: Stream Info via Helix API (3 issues)

- [ ] Add Helix API client to fetch stream title, game, viewer count, uptime
- [ ] Add `StreamInfo` entity + periodic refresh (every 30–60 seconds)
- [ ] Display stream info in Control UI (title, game, viewers, uptime)
- [ ] Write unit tests for stream info fetching and caching

**Deliverable**: Stream metadata visible in Control UI in real time.

---

## 8. Migration & Compatibility

### 8.1 Data Migration

1. **Existing Chat**: Existing IRC chat logs (if any exist) should be migrated to `ChatMessage` with a placeholder `ChatFragment[]` (plain TextFragment only).
2. **Tokens**: `TwitchToken` entity persists; `TwitchTokenManager` continues to use it.
3. **Backward Compat**: `ChatEvent.Content` remains and is always populated; UI code can continue using plain text until Teleprompter/Overlay are updated to use fragments.

### 8.2 Rollback Path

If EventSub fails to connect after phase 17:
1. Temporarily fall back to IRC for chat ingestion (old `TwitchService` logic)
2. Parse IRC PRIVMSG lines; create `ChatMessage` entries with plain-text fragments
3. Re-enable IRC until EventSub is debugged

This fallback is not automatic; operator must manually toggle via config.

### 8.3 OAuth Transition

**Config-based credentials** (current) → **Inline OAuth tokens** (new):
- Old `appsettings.json` entries (e.g., `TwitchOptions:Channel`, `TwitchOptions:OAuthToken`) are **deprecated** (logged as warning)
- If no stored token exists AND no config token is provided, system prompts for login
- If stored token exists, config is ignored

---

## 9. Open Questions

### 9.1 Bot Moderator Status

**Question**: Does the bot user have moderator status in the broadcaster's channel?

**Impact**: 
- **Yes**: Use `moderator:read:followers` scope in EventSub to receive follow events natively
- **No**: Follow events are unavailable via EventSub; fallback to IRC or polling required

**Recommendation**: Confirm before Phase 19 implementation.

### 9.2 Cheer Bits Threshold for Gold Flash

**Question**: What bits threshold triggers the "gold flash" overlay effect?

**Examples**:
- ≥ 1 bit = flash (every cheer visible)
- ≥ 100 bits = flash (only significant cheers highlighted)
- ≥ 1000 bits = flash (major cheers only)
- Configurable per operator preference

**Recommendation**: Decide before Phase 19 implementation.

### 9.3 Animated vs. Static Emotes

**Question**: Default to animated or static emotes?

**Options**:
- Animated (motion-heavy, 2-3x file size)
- Static (less distraction, smaller payload)
- Configurable operator preference via Control UI

**Recommendation**: Provide operator toggle in Phase 18, default to static.

---

## 10. Links & References

### Related Architecture Docs

- **[overview.md](./overview.md)** — High-level system architecture, module layout, platform abstractions
- **[authentication.md](./authentication.md)** — OAuth2 flow, token management, scope validation (to be written)
- **[event-system.md](./event-system.md)** — PlatformEvent hierarchy, event bus design (to be written)

### Code Locations

| File | Purpose |
|------|---------|
| `src/Thiccdal.Remote.Twitch/TwitchService.cs` | Main Twitch integration (to be rewritten for EventSub) |
| `src/Thiccdal.Remote.Twitch/TwitchTokenManager.cs` | OAuth2 token lifecycle; to be enhanced for inline OAuth |
| `src/Thiccdal.Data/ApplicationDbContext.cs` | EF Core context; add new entities here |
| `src/Thiccdal.Infrastructure/Interfaces/IChatService.cs` | Chat service contract; enhance for fragments |
| `src/Thiccdal.Modules.Teleprompter/Pages/Prompter.razor` | UI rendering; use `ChatFragment[]` here |
| `src/Thiccdal.Modules.Overlay/Pages/Overlay.razor` | Overlay rendering; use `ChatFragment[]` here |

### External References

- **[Twitch EventSub Documentation](https://dev.twitch.tv/docs/eventsub)** — Official EventSub spec
- **[Twitch API Scopes](https://dev.twitch.tv/docs/authentication/scopes)** — OAuth scope reference
- **[Twitch Emote CDN](https://dev.twitch.tv/docs/api/reference#get-emote)** — Emote metadata and CDN URLs

---

## 11. Decision Summary

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| **Chat Protocol** | EventSub WebSocket (not IRC) | Official, encrypted, metadata-rich, automatic reconnection |
| **Chat Fragments** | Hierarchy (TextFragment, EmoteFragment, CheermoteFragment, BadgeFragment) | Structured rendering, forward-compatible, embeds emote/badge metadata |
| **Emote Rendering** | Teleprompter + Overlay use CDN URLs from EventSub metadata | Deterministic, no extra HTTP call, operator-configurable (static/animated) |
| **Authentication** | Inline OAuth (not config-based credentials) | User-friendly, no manual credential entry, secure token refresh |
| **Event Persistence** | All events → `PlatformEvent` entity (backward compat with `ChatEvent.Content`) | Auditable, queryable, supports future analytics and replay |
| **Phasing** | 4 phases (Foundation, Fragments, Coverage, Stream Info) | Clear milestones, testable at each stage, separates concerns |

---

## 12. Approval & Sign-Off

**Decision Owner**: Mal (Lead / Orchestrator)  
**Requested By**: ThindalTV  
**Status**: Ready for Phase 17 implementation  
**Review Date**: 2026-05-28  

---

**End of Document**
