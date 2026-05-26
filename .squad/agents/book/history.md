# Project Context

- **Project:** Thiccdal
- **Requested by:** ThindalTV
- **Shape:** Twitch bot and streaming command-and-control system
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite, Aspire
- **Architecture:** `docs\architecture\overview.md`

## Core Context

Book owns user-facing documentation rather than developer architecture guidance.

## Recent Updates

📌 Firefly squad configured on 2026-05-27

## Learnings

- `docs\help\` is the primary destination for Book's output.
- Architecture docs exist for context, but Book should write for operators and users.
- Twitch connection in Thiccdal uses **inline OAuth 2.0** (user-initiated login via browser, not config-based credentials).
- Token manager handles automatic refresh; operators see simple UI states: Not Connected → Connecting → Connected → Error.
- Required scopes: `user:read:chat`, `chat:read`, `chat:edit`, and conditionally `moderator:read:followers` (for follower events if bot is moderator).
- Connection states: `NotAuthorized` (no token), `Authorized` (token exists), `Connecting`, `Connected`, `Disconnected`, `Error`.
- Token expiration is handled transparently; no user action needed unless token refresh fails or bot account loses moderator status.

## Work Completed

### 2026-05-28: Twitch Connection Documentation Written

**What**: Created `/docs/help/connecting-to-twitch.md` — comprehensive user guide covering:
- Quick-start authentication flow (click badge → login → approve scopes → connected)
- What the UI shows at each stage
- OAuth flow breakdown (login, permission review, authorization, redirect)
- Token lifecycle (4-hour expiry, automatic refresh, refresh token)
- Disconnection and account switching
- Troubleshooting: popup blocking, auth failures, refresh failures, scope denials, token not found
- Permission scope reference table
- Security & privacy summary
- Next steps after connection

**Why**: Operators need clear, practical guidance on how to authenticate without diving into OAuth internals. Troubleshooting section covers the most common failure points observed in the token manager code.

**Status**: Complete. Documentation is operator-focused, not architecture-facing. Covers shipped behavior only (inline OAuth, token refresh, scope validation).
