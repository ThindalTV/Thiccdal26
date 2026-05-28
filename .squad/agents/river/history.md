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
- The smallest clean Twitch live indicator seam is a boolean `ITwitchService.IsStreamLive` plus a `StreamLiveStateChanged` event and `RefreshStreamState()`, backed by Helix `/streams` with `BroadcasterId` rather than a larger stream-info model.
- `ITwitchTokenManager.GetToken()` now returns `null` when no Twitch token has ever been stored, and startup paths in `src\Remote\Thiccdal.Remote.Twitch\TwitchService.cs` must treat that as the explicit first-run `NotAuthorized` state instead of throwing.
- The first-run no-token regression is covered in `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchTokenManagerTests.cs` and `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchServiceTests.cs`; real token refresh and network failures still remain exception paths.
- Twitch startup wiring now lives in `src\Remote\Thiccdal.Remote.Twitch\TwitchRegistrationExtensions.cs`; `Program.cs` should call `AddTwitchIntegration(builder.Configuration)` and `MapTwitchEndpoints()` rather than owning Twitch callback or DI details.
- Platform-specific DI ownership belongs in the adapter project, not feature modules; `src\Modules\Thiccdal.Modules.ChatBot\ChatBotRegistrationExtension.cs` now stays platform-agnostic and the module no longer references `Thiccdal.Remote.Twitch`.
- `TwitchConnectionMonitor` is registered from the Twitch adapter beside `ITwitchService` and `ITwitchTokenManager`, so OAuth callbacks can refresh both the service state machine and the generic integration monitor from one boundary.
- Extraction coverage lives in `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchRegistrationExtensionsTests.cs`, alongside the existing Twitch token/service tests.
- Runtime-editable Twitch channel targeting should persist in SQLite, not by rewriting `appsettings.json`; the UI-safe seam is now `src\Thiccdal\Services\TwitchSettingsService.cs` delegating to `src\Remote\Thiccdal.Remote.Twitch\TwitchTargetChannelService.cs`.
- Keep Twitch bot identity and target broadcaster identity explicit: `TwitchOptions.BotUsername` identifies the authenticated bot account, while `DefaultTargetChannel`/`DefaultBroadcasterId` seed the target channel owner until a UI override is saved.
- `TwitchService` should always resolve a `TwitchChatConnectionProfile` before connecting or calling Helix so IRC login uses the bot username while stream-state lookups use the target broadcaster ID; live target switches flow through `ITwitchTargetChannelService.ConnectionProfileChanged`.
- Verification for configurable target-channel work: `dotnet build src\Thiccdal\Thiccdal.csproj`, `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj`, and `dotnet test src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj`.
- Phase 17 Helix foundation now has a dedicated seam: `src\Thiccdal.Infrastructure\Twitch\ITwitchHelixClient.cs` with `src\Remote\Thiccdal.Remote.Twitch\TwitchHelixClient.cs` owning Helix REST calls instead of embedding raw HTTP in `TwitchService`.
- `TwitchService` still keeps IRC receive/connect behavior for now, but `RefreshStreamState()` and `SendMessage()` should prefer the typed Helix client; outbound chat falls back to IRC only when Helix cannot be used yet.
- Twitch config now carries Helix/EventSub foundation knobs in `src\Thiccdal.Infrastructure\Twitch\TwitchOptions.cs`, `TwitchHelixOptions.cs`, and `TwitchEventSubOptions.cs`; moderator-required EventSub and animated emotes are the defaults, and OAuth scopes are configured from options rather than a hard-coded string.
- Coverage for the Helix foundation slice lives in `src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\TwitchHelixClientTests.cs`, `TwitchServiceTests.cs`, and `TwitchRegistrationExtensionsTests.cs`.

### 2026-05-28–2026-05-29: Helix EventSub Architecture + Auth Surface (Archived)

Helix EventSub redesign locked: Pure EventSub WebSocket (not hybrid), ChatFragment hierarchy, new OAuth scopes, Emote CDN URLs. Phase 17 ownership: TwitchOptions expansion, TwitchHelixClient, EventSub WebSocket manager, scope-upgrade re-auth, ITwitchEventSubClient definition. Twitch auth surface delivered: `TwitchConnectionState` enum, `ITwitchService` upgraded (ConnectionState, ConnectionStateChanged, RefreshConnectionState), `ITwitchTokenManager.HasToken()` no-throw, TwitchOptions.BroadcasterId added. DI singleton pattern: register concrete type once, forward to interfaces. Admin UI: PlatformStatusButton + TwitchAuthDialog at /integrations. 22 tests passing. Complementary integration monitor (Kaylee) and dashboard UI (Inara) confirmed no conflicts.

ITwitchHelixClient seam delivered: typed Helix client decouples REST calls from service. TwitchService routes RefreshStreamState + SendMessage through Helix client; IRC retained for inbound fallback only. Payload mapper isolates EventSub notification parsing. Activity feed (IActivityFeedService) provides shared boundary for overlay/prompter. Raw Twitch payloads persisted via PlatformEventRecord before dispatch.
- EventSub inbound is now owned by `src\Remote\Thiccdal.Remote.Twitch\TwitchEventSubClient.cs`, with payload mapping isolated in `TwitchEventSubNotificationMapper.cs` and subscription CRUD living in `TwitchHelixClient.cs`.
- Rich Twitch chat should stay platform-agnostic at the consumer boundary: `ChatEvent` now carries `ChatMessagePart`, `ChatBadge`, color, and HTML fallback, while typed follow/subscribe/cheer/raid/redeem records derive from `PlatformEvent`.
- The fastest safe bridge to overlay/prompter is the shared activity feed seam (`IActivityFeedService` + `PlatformActivityFormatter`), which lets Twitch events surface downstream before a formal `IEventBus` exists.
- Raw Twitch payloads are now persisted through `src\Thiccdal.Data\Models\PlatformEventRecord.cs` and dispatched from `TwitchService` only after persistence, giving downstream consumers diagnostics without depending on Helix/EventSub JSON directly.
- Validation for the Helix/EventSub slice currently means: `dotnet build src\Thiccdal\Thiccdal.csproj`, `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj`, and `dotnet test src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj`.
- Phase 6 YouTube now follows the same persistence seam as Twitch: the adapter stays on `Thiccdal.Infrastructure`, publishes via `IEventBus`, and leaves EF/storage ownership inside `src\Thiccdal.Data\`.
- When a normalized event needs both a shared enum and the vendor's exact type string, persist both: `PlatformEvent.Type` stays normalized while `PlatformEvent.SourceEventType` carries values like `textMessageEvent` or `channel.follow`.
- YouTube chat persistence depends on item-level raw JSON with `authorDetails.channelId` at the root; `src\Tests\Thiccdal.Data.Tests\ChatPersistenceServiceTests.cs` is the guardrail for that seam.
- Current integration validation for this slice is: `dotnet build src\Thiccdal\Thiccdal.csproj`, `dotnet test src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\Thiccdal.Remote.YouTube.Tests.csproj`, `dotnet test src\Tests\Remote\Thiccdal.Remote.Twitch.Tests\Thiccdal.Remote.Twitch.Tests.csproj`, and `dotnet test src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj`.

### 2026-05-29: Phase 6 YouTube completion

- `src\Remote\Thiccdal.Remote.YouTube\` now stays infrastructure-first by consuming `IYouTubeTokenStore` from `src\Thiccdal.Infrastructure\YouTube\`; the adapter project no longer references `Thiccdal.Data` directly.
- For poll-based platforms, preserve per-item raw JSON and set `SourceEventType` on every normalized event so unknown vendor message types can stay diagnosable without misclassifying them as chat.
- YouTube typed runtime events now live in `src\Thiccdal.Infrastructure\Bot\Models\SuperChatEvent.cs` and `MembershipEvent.cs`, while persisted TPH counterparts and migrations live in `src\Thiccdal.Data\Models\` and `src\Thiccdal.Data\Migrations\`.
- Phase 6 verification commands: `dotnet build Thiccdal.slnx --no-restore`, `dotnet test src\Tests\Remote\Thiccdal.Remote.YouTube.Tests\Thiccdal.Remote.YouTube.Tests.csproj --no-restore`, `dotnet test src\Tests\Thiccdal.Data.Tests\Thiccdal.Data.Tests.csproj --no-restore`, and `dotnet test src\Tests\Thiccdal.Tests\Thiccdal.Tests.csproj --no-restore --filter "FullyQualifiedName~StatusEndpointTests|FullyQualifiedName~StreamStatusServiceTests|FullyQualifiedName~NullPlatformFullStackTests"`.
