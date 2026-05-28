# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Zoe handles GitHub coordination, work-item clarity, and delivery status.

## Recent Updates

📌 Firefly squad configured on 2026-05-27  
📌 Initial GitHub backlog scan completed on 2026-05-26
📌 Issue routing completed on 2026-05-28: All 152 open issues labeled with squad and squad:{member} tags

## Learnings

- Zoe owns issue and PR hygiene, but Ralph owns continuous board monitoring.
- Zoe should make status legible for the team and the user.
- **Streaming implementation seam:** Phase 8 (#74-#80) was scaffolded as a control plane (API + config persistence) without implementing the data plane (RTMP ingest, fanout, BRB injection, disk recording). When auditing issues that depend on unfinished work, comment each with the exact missing scope and blocker chain, then leave open rather than close prematurely. See `.squad/decisions/inbox/zoe-assess-phase8-closures.md` for the blocker chain.
- **Phase 7 deferral pattern (2026-05-27):** When deferring work intentionally, add `status/deferred-phase7` label, comment on each issue explaining the deferral and Phase 6 priority, and document the decision in `.squad/decisions/inbox/zoe-{topic}-deferral.md`. This keeps the backlog truthful and visible without closing legitimate but deferred issues.
- **Product direction alignment (2026-05-28):** Chat surface consolidates on the prompter (operator focus). Dashboard chat feed (#100) intentionally not built — close as designed-out feature, not missed implementation. Question queue flash (#103) implemented on both dashboard and prompter. Keep GitHub in sync with actual product direction, not original spec.
- **Project structure:** .NET Aspire-based system; Blazor Server host controls platform adapters (Twitch, YouTube, Discord, Facebook, X). Streaming (RTMP relay), overlay SignalR, teleprompter UI, and operator control UI are the main modules.
- **Issue phases:** 11 (overlay), 12 (status API), 13 (identity merge), 14 (hardening), 15 (operator state), 16 (pre-live checklist) — currently at phase 16.
- **PR board:** Zero open PRs (clean state, ready for new work).
- **Backlog status:** 152 open issues across phases, all now routed to squad members:
  - **squad:inara** (48 issues): UI/UX work — operator ui, overlay, teleprompter
  - **squad:kaylee** (28 issues): Backend work — EF Core, data, chat services, APIs, streaming
  - **squad:mal** (48 issues): Testing and architecture — all test issues plus architectural decisions
  - **squad:river** (28 issues): Platform integrations — Twitch, YouTube, Discord, Facebook, X, TikTok, LinkedIn
- **Routing logic:** Issues routed by primary area of concern using `area/*`, `type/*`, and `phase/*` labels as signals. Type/test issues go to Mal (testing coordination). UI-related work goes to Inara. Backend services go to Kaylee. Platform-specific integration work goes to River. Infrastructure/architecture work goes to Mal.
- **Helix issue hygiene:** For Twitch Phase 17+ work, close only issues whose original acceptance text is fully satisfied by landed code. Use progress comments instead of closure when the repo has partial scope shifts (`#167`, `#169`, `#171`, `#96` were all partials during the 2026-05-29 audit).
- **Helix audit sources:** Post-batch issue status checks should read `docs\architecture\helix-redesign.md`, `.squad\log\2026-05-29T00-00-00Z-helix-foundation.md`, `.squad\log\2026-05-29T01-10-00-twitch-auth-batch.md`, and the matching River/Kaylee/Inara orchestration logs before touching GitHub.
- **Validation baseline:** `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj --no-restore` passed with 41 tests and `dotnet test src\Tests\Thiccdal.Tests\Thiccdal.Tests.csproj --no-restore` passed with 3 route tests during the 2026-05-29 audit. Full `dotnet build Thiccdal.slnx -warnaserror` was blocked by a running `Thiccdal.Aspire.AppHost.exe` file lock, so treat that specific failure as environment contention rather than immediate code drift.
- **Operator UI redesign (2026-05-30, phase 10.1):** TopBar.razor has evolved from the original spec. The unified top bar now features: platform indicators (left), restream destinations with per-platform bitrate/fps stats and go/stop buttons (center), and timer/stream info/end actions (right). Dashboard.razor implements 3-column layout with Prompter+BotCommands (left), QuestionQueue (center), LowerThird+OverlayGallery (right). Mode-switching UI (Pre-Live checklist vs. Live operations) is structurally ready but conditional rendering logic is still in progress. Updated issue #93 to reflect actual implementation state.
- **Issue alignment pattern (2026-05-30):** When issue descriptions become misaligned with implementation (e.g., referencing old design instead of current code), reframe future refinement sections to explicitly cite the current structure as the *basis*, not as a placeholder. This keeps scope realistic for dependent issues and clarifies that refinement is enhancement, not replacement. Documented in `.squad/decisions/inbox/zoe-phase10-live-mode-basis.md`.
- **Scope clarity pattern (2026-05-30):** When future refinement sections duplicate or dilute focus from the core acceptance criteria, consider removing them in favor of keeping only the active-phase enhancements (e.g., Pre-Live in issue #93). This keeps GitHub issues focused and prevents scope sprawl. Removed "Live Mode Regions (future refinement)" from issue #93 per ThindalTV directive; documented in `.squad/decisions/inbox/zoe-remove-live-mode-refinement.md`.

### 2026-05-28: Issue #92 GitHub Text Update & Closure Prep

**What was done:**
- Updated GitHub issue #92 to reflect actual shipped implementation of mention-gated AI replies
- Removed outdated references: non-existent `AiFreeFormHandler` class, old flat `ChatbotOptions` config shape, wildcard `"*"` command trigger
- Added current implementation details: nested `ChatBotOptions.AiResponder`, service registration as `IChatBotAiResponder`, mention-gating regex in `CommandDispatcher`, origin-only chatter memory integration, safety controls (5-second timeout, output normalization)
- Updated all acceptance criteria checkboxes to `[x]` (complete)
- Left issue open pending re-review from Jayne

**Why:** Issue descriptions drift from implementation over time. Keeping GitHub text in sync prevents downstream teams from chasing phantom features and clarifies what's actually shipped for future refinement work.

**Blocker released for next phase:** Jayne's security re-review approved issue #92 for closure; no blocking issues remain.

**Key files:**
- `src/Thiccdal.Infrastructure/Bot/ChatBotAiResponderOptions.cs` (config)
- `src/Modules/Thiccdal.Modules.ChatBot/Services/ChatBotAiResponder.cs` (service)
- `src/Modules/Thiccdal.Modules.ChatBot/Services/CommandDispatcher.cs` (dispatch routing)
- `src/Modules/Thiccdal.Modules.ChatBot/Services/ChatServiceCommandResponseSink.cs` (origin-only routing)

### 2026-05-31: Issue #129 Closed – Chat Display Name Alignment

**What was done:**
- Reassessed issue #129 (identity merge UI) based on Inara's overlay chat rendering fix and Kaylee's backend chat persistence work
- Verified that canonical identity names now render consistently across all chat surfaces:
  - **Backend:** ChatEvent.PreferredAuthor carries merged UserIdentity.DisplayName, resolved during chat persistence in ChatPersistenceService.ResolvePreferredAuthor()
  - **Overlay:** ChatFeedOverlayComponent renders DisplayAuthor (canonical) instead of raw Author
  - **Activity Feed:** PlatformActivityFormatter.CreateChatEntry() uses DisplayAuthor for all feed entries
  - **Tests:** ChatFeedOverlayComponentTests and ActivityFeedServiceTests confirm the seam
- Closed issue #129 as complete

**Why:** The original gap ("chat/render paths did not prefer UserIdentity.DisplayName when available") is now fully addressed. All chat renderers use the DisplayAuthor property, which prioritizes PreferredAuthor (set from UserIdentity.DisplayName during persistence) over raw Author.

**Key files:**
- `src/Thiccdal.Data/ChatPersistenceService.cs` — ResolvePreferredAuthor() method resolves canonical display name from UserIdentity
- `src/Thiccdal.Infrastructure/Bot/Models/ChatEvent.cs` — DisplayAuthor property and PreferredAuthor field
- `src/Modules/Thiccdal.Modules.Overlay/Components/ChatFeedOverlayComponent.razor` — uses DisplayAuthor (line 16)
- `src/Thiccdal.Infrastructure/Bot/PlatformActivityFormatter.cs` — CreateChatEntry() uses DisplayAuthor (line 57)
- `src/Tests/Thiccdal.Tests/ChatFeedOverlayComponentTests.cs` — test coverage for overlay rendering
- `src/Tests/Thiccdal.Tests/ActivityFeedServiceTests.cs` — test coverage for feed rendering
