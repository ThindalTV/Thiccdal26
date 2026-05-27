# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Jayne owns security review, hardening, and pen-testing style analysis.

## Recent Updates

📌 Firefly squad configured on 2026-05-27

## Learnings

- Security work will likely center on secrets, auth boundaries, and external platform integrations.
- Jayne is also the natural reviewer for high-risk changes.

### 2026-05-28: Twitch OAuth Auth Flow Hardening

**What was done:**
- Added CSRF state parameter to `GetAuthorizationUrl()` (256-bit URL-safe random, 10-min TTL, ConcurrentDictionary)
- Added `ValidateAndConsumeState(string)` to `ITwitchTokenManager` interface + implementation
- Fixed OAuth callback in `Program.cs`: nullable params, state validation, error redirect on `?error=...`
- Fixed `StoreToken()` to upsert (remove existing before insert)
- Fixed `Revoke()` to call `POST /oauth2/revoke` at Twitch before deleting locally (5s timeout, best-effort)
- 22 tests pass including 6 new state/upsert tests

**Key file paths:**
- `src/Thiccdal.Infrastructure/Twitch/ITwitchTokenManager.cs` — interface (added `ValidateAndConsumeState`)
- `src/Remote/Thiccdal.Remote.Twitch/TwitchTokenManager.cs` — implementation
- `src/Thiccdal/Program.cs` — OAuth callback endpoint
- `src/Tests/Remote/Thiccdal.Remote.Twitch.Tests/TestProject1/TwitchTokenManagerTests.cs`

**Deferred risks (written to inbox):**
- Token encryption at rest (DPAPI) — needs team decision on process account scope
- Duplicate auth dialog cleanup (TwitchAuthDialog.razor + TwitchConnect.razor vs new IntegrationAuthDialog)
- Exception swallowed in TopBar.razor (no ILogger, catch swallows silently)
- Exception message leaked to UI in TwitchConnect.razor

**Patterns to remember:**
- OAuth state tokens: singleton service, ConcurrentDictionary with DateTime expiry, one-time consume
- Revoke: always best-effort API call first, then local DB remove regardless of API result
- `TwitchTokenManager` is registered as singleton — safe to hold state in-process
- Main app build has a pre-existing OpenTelemetry NU1902 vulnerability error in ServiceDefaults — unrelated to auth work
- This pattern (IntegrationConnector + IntegrationAuthDialog) is designed for reuse across all platforms (YouTube, Kick, etc.)

### 2026-05-29: Batch Completion — Twitch Auth + Integration Surface

**Team summary:**
- All OAuth hardening fixes are committed and tested
- River's `ITwitchService` state machine is the single source of truth for connection state
- Inara's UI components properly consume the state machine via events (no polling)
- Kaylee's `IIntegrationConnectionMonitor` is complementary (DB-only monitor for platform enumeration)
- 22 tests passing; zero security regressions

**Deferred risks revisited:**
- Token encryption at rest (DPAPI): Still deferred; team decision on process account scope
- Duplicate auth dialog cleanup: Still deferred; may retire old TwitchAuthDialog.razor when Control module is primary
- TopBar.razor exception handling: Still deferred; needs ILogger injection (low priority)
- TwitchConnect.razor exception leak: Still deferred; should use generic user message

**Status:** ✅ Security review complete. All hardening fixes committed. 22 tests passing. Deferred risks documented for Phase 17+ team review.

### 2026-05-29: AI Response Routing — Cross-Platform Mirroring Analysis

**What was decided:**
- AI mention replies MUST stay on the originating platform only
- Cross-platform mirroring of AI responses is unsafe and creates multiple security/moderation risks
- Written to `.squad/decisions/inbox/jayne-ai-routing-decision.md`

**Security rationale:**

1. **Abuse amplification:** Single mention on one platform → AI replies broadcast to 5+ platforms simultaneously → 5-10x spam multiplication
2. **Moderation context loss:** AI replies appear on platforms where the triggering mention isn't visible → false positive bans
3. **Trust boundary violation:** Each platform is a separate community; users on Twitch shouldn't trigger bot actions on Discord
4. **Spam relay risk:** Incoming chat repost + AI reply mirroring creates multiplicative message growth and potential feedback loops
5. **Operator accountability:** AI-generated content represents stream brand and should be explicitly scoped per platform

**Key distinction:**
- ✅ **Incoming chat mirroring (ChatRepostService):** Safe and appropriate — human messages are transparent, prefixed with origin, and self-moderated by platform TOS
- ❌ **AI reply mirroring:** Unsafe — bot-generated content lacks context on non-origin platforms, violates moderation boundaries, and amplifies abuse surface

**Implementation requirement:**
- Add `PlatformEventSource` to `CommandContext` (currently only has display string)
- Update `ChatServiceCommandResponseSink` to route AI replies to originating `IPlatformConnection` only
- Replace broadcast `IChatService.SendMessage(...)` with platform-specific `IPlatformConnection.SendMessage(...)`

**Key file paths:**
- `src/Modules/Thiccdal.Modules.ChatBot/Services/ChatRepostService.cs` — incoming chat mirroring (safe)
- `src/Modules/Thiccdal.Modules.ChatBot/Services/CommandDispatcher.cs` — AI fallback dispatch via `SendAiFallback(...)`
- `src/Modules/Thiccdal.Modules.ChatBot/Services/ChatServiceCommandResponseSink.cs` — current broadcast sink (unsafe for AI)
- `src/Modules/Thiccdal.Modules.ChatBot/Services/ChatAggregationService.cs` — `SendMessage(...)` broadcasts to all connected platforms
- `src/Thiccdal.Infrastructure/Bot/CommandContext.cs` — needs structured `PlatformEventSource` field

**Pattern to remember:**
- **Chat aggregation vs AI routing:** Incoming chat can safely mirror cross-platform because it's transparent and human-generated. AI replies must respect platform boundaries because they're bot-generated, lack context on non-origin platforms, and amplify abuse surface.
- **Trust boundaries:** Each platform is a separate trust domain. Actions triggered on one platform should not automatically propagate to others unless explicitly designed for cross-platform coordination (like chat mirroring with clear origin labels).

**Status:** 🔴 **BLOCKING** — AI cross-platform mirroring must be prevented before chatbot ships (relates to Issue #92 final review rejection).

### 2026-05-30: AI Chatter Memory Guardrails

**What was decided:**
- AI responder **can** have chatter memory, but only as a tightly scoped, minimization-first feature
- Do **not** feed raw chat history, `RawData`, or cross-platform identity joins into prompts
- Memory must be public-chat only, platform-scoped, auto-expiring, operator-disclosed, and easy to disable
- Wrote the team decision to `.squad/decisions/inbox/jayne-chatter-memory.md`

**Security constraints to enforce:**
1. **Retention:** Store only a tiny per-chatter summary with strict TTL; do not create indefinite dossiers
2. **Consent / expectation:** Keep it off by default until disclosed; only use data users reasonably expect from public chat
3. **Scope:** Memory must stay within the same platform/channel and must not follow a user across Twitch/YouTube/Discord
4. **Prompt use:** Inject only sanitized summary fields, never raw transcripts, HTML, payload blobs, secrets, or moderation notes
5. **Abuse resistance:** Models must not write memory directly; use allowlisted extraction and block storage of sensitive or toxic content

**Relevant code reality:**
- `ChatBotAiResponder` currently sends only a system prompt plus the current viewer message to `IChatCompletionClient`
- `ChatPersistenceService` persists `Content`, `HtmlContent`, and `RawData` for chat events, so prompt retrieval from raw history would materially widen privacy risk
- `PlatformUser` is already keyed by `(Source, PlatformUserId)`, which supports safe platform-scoped memory if the team adds it later

**Key file paths:**
- `src/Modules/Thiccdal.Modules.ChatBot/Services/ChatBotAiResponder.cs` — current AI prompt assembly
- `src/Thiccdal.Infrastructure/AI/AiChatCompletionRequest.cs` — AI request surface
- `src/Thiccdal.Data/ChatPersistenceService.cs` — persisted chat content and raw payloads
- `src/Thiccdal.Data/Models/ChatMessage.cs` — stored chat/raw/html fields
- `src/Thiccdal.Data/Models/PlatformUser.cs` — platform-scoped chatter identity anchor

**Pattern to remember:**
- **AI memory must be derived, not replayed.** Persist a tiny, sanitized summary with expiry instead of handing the model raw historical chat.
- **Public chat does not equal unlimited reuse.** Even when data was visible in chat, reusing it later in AI prompts changes the privacy and moderation risk profile.

### 2026-05-27: Chatter Memory Security Re-Review & Approval for Shipping

**Review scope:**
- Comprehensive re-review of Mal's revised chatter-memory implementation
- Verified both blockers resolved: operator-facing reset path + non-destructive reset semantics
- Re-verified all six security guardrails remained intact

**Blockers resolved:**
1. **✅ Real operator-facing reset path:** Main nav includes Chatbot entry (NavMenu.razor:53-56); /chatbot page exposes scoped + global reset controls wired to IChatterMemoryService.Reset(...) and ResetAll(...) (Chatbot.razor:1-15, 56-137); route coverage confirms page renders with reset controls (RouteRenderingTests.cs:69-80)
2. **✅ Non-destructive reset semantics:** Interface contract explicitly preserves source chat history (IChatterMemoryService.cs:24-46); Reset(...) and ResetAll(...) write markers not delete records (ChatterMemoryService.cs:132-184); memory reads honor reset cutoff (ChatterMemoryService.cs:197-213); tests verify chat/event row counts unchanged after reset (ChatterMemoryServiceTests.cs:58-105)

**Six security guardrails re-verified:**
1. **Strict {platform, channel, user} scoping** — Lookup keyed by platform source + platform user ID (ChatterMemoryService.cs:81-109); channel filtered on platform event; PlatformUser uniqueness enforced {Source, PlatformUserId} (ApplicationDbContext.cs:130-145) ✅
2. **Public-info-only derived memory** — Facts built from ChatMessage.Content only (ChatterMemoryService.cs:215-341); sanitized for sensitive markers/URLs/tokens before prompt use; no transcripts, moderation notes, internal payloads ✅
3. **No RawData/HtmlContent/transcript leakage** — Memory builder uses sanitized derived facts; AI prompt injects only DisplayName, LastInteractionAt, Facts (ChatBotAiResponder.cs:170-186); no memory-path reads of RawData or HtmlContent found ✅
4. **No cross-platform identity merging** — Lookup remains platform-qualified (ChatterMemoryService.cs:81-85); no join-by-display-name identity stitching; unique index on {Source, PlatformUserId} enforced (ApplicationDbContext.cs:130-132) ✅
5. **AI replies stay on originating platform/channel** — CommandDispatcher carries typed origin metadata into CommandContext (CommandDispatcher.cs:213-225); ChatServiceCommandResponseSink routes only to matching platform via context.ChannelId (ChatServiceCommandResponseSink.cs:32-50); coverage exists (ChatServiceCommandResponseSinkTests.cs:11-34) ✅
6. **Reset is real and non-destructive** — Immediately suppresses older context through reset barriers (ChatterMemoryService.cs:132-213); source records remain intact for audit trail + recovery (ChatterMemoryServiceTests.cs:58-105) ✅

**Test results:**
- Thiccdal.Data.Tests: 37/37 ✅
- Thiccdal.Tests (ChatBot, routing, components): 29/29 ✅

**Approval rationale:**
- Both blocking issues resolved
- Operator-facing reset is real, wired, and UI-discoverable
- Reset is non-destructive by design; audit trail preserved
- All six guardrails remain intact and enforced
- Complete test coverage; no regressions
- Ready for shipping to production

**Non-blocking future watch item:**
- Channel-aware outbound adapter overrides worth enforcing before any true multi-channel-per-platform send feature ships

**Verdict:** ✅ **APPROVE FOR SHIPPING**

**Orchestration log:** .squad/orchestration-log/2026-05-27T22-55-44Z-jayne-chatter-memory-rereview.md

**Status:** Chatter-memory approved for production deployment.

### 2026-05-28: Issue #92 Final Security Re-Review & Closure Approval

**What was done:**
- Performed final re-review of GitHub issue #92 after Zoe's implementation alignment update
- Verified blocking issue resolution: AI replies now route **only to originating platform/channel** via `CommandContext.SourcePlatform` + `ChannelId` + `ChatServiceCommandResponseSink` pattern
- Confirmed issue body accurately describes shipped implementation: nested config, mention-gating regex, origin-only chatter memory, 5-second timeout, normalized output
- Re-verified all six security guardrails intact and enforced:
  1. ✅ Strict {platform, channel, user} scoping in memory derivation
  2. ✅ Public-info-only memory (no RawData, HtmlContent, transcripts)
  3. ✅ No cross-platform identity merging
  4. ✅ AI replies constrained to originating platform/channel
  5. ✅ Reset semantics non-destructive (operator-facing controls intact)
  6. ✅ All guardrails remain intact and enforced
- Validated test coverage: 115 tests in Thiccdal.Tests, 37 tests in Thiccdal.Data.Tests, ChatBot module builds clean

**Why:** Issue #92 was critical blocker on previous review due to cross-platform mirroring risk. Zoe's implementation alignment update + shipped routing fix provide confidence for closure. Final security re-review clears last blocker before GitHub closure.

**Approval decision:** ✅ **APPROVED FOR CLOSURE**

**Rationale:**
- Cross-platform mirroring blocker is fully resolved via origin-only routing
- Issue body now matches actual shipped implementation
- All acceptance criteria complete and verifiable
- No remaining security blockers identified
- Test coverage complete; zero regressions
- Prior concerns (AI reply safety, chatter memory isolation) remain addressed

**Note:** Issue body simplifies one dispatch detail (unknown/disabled `!` commands also fall through to AI fallback), but responder remains mention-gated, so shipped behavior stays within described feature scope.

**Handoff:** Zoe ready to close issue #92 on GitHub

**Orchestration log:** .squad/orchestration-log/2026-05-28T01-17-55-jayne.md

**Status:** Issue #92 ready for GitHub closure; no blocking security issues remain.

