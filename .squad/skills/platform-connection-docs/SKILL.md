# Writing User Documentation for Platform Connections

**Skill Owner**: Book (User Documentation)  
**Created**: 2026-05-28  
**Context**: Thiccdal integrates with multiple streaming platforms (Twitch, YouTube, Discord, etc.). Each platform has an OAuth flow, connection states, and potential failure modes. This skill captures the pattern for writing operator-friendly connection docs.

---

## Pattern

### Structure

Each platform connection guide should follow this outline:

1. **Overview** — One paragraph explaining what OAuth is (for first-time users) and what granting permissions means
2. **Quick Start** — 5–7 numbered steps from "open dashboard" to "connected"
3. **What You'll See** — Three substates: Before (indicator is off), During (connecting), After (indicator is on)
4. **The Login Flow** — Step-by-step breakdown of the OAuth browser flow: click button → Twitch login → approve permissions → redirect back
5. **Token Lifecycle** — How long tokens last, what refresh means, how automatic refresh works
6. **Checking Connection Status** — Where to look in the UI to verify connected state; optional advanced (console) method
7. **Disconnecting or Re-authenticating** — How to log out, how to switch accounts
8. **Troubleshooting** — Organized by symptom:
   - Popup blocked
   - Login window doesn't open
   - Authorization failed / red error badge
   - Token refresh failed
   - Permission denied for optional scope
   - Token not found on startup
9. **Permission Scope Reference** — Table: Scope → What It Does → Why Thiccdal Needs It
10. **Security & Privacy** — Reassure operator: creds never stored, token only, auto-revocation, read-only access
11. **What's Next** — Point to related docs (chat config, overlays, bot commands)
12. **Still Have Questions?** — Generic troubleshooting steps (console check, connected apps audit, restart)

### Tone

- **Direct and task-oriented**: "Click the button" not "You may wish to consider clicking the button"
- **Reassuring**: Normalize the OAuth flow; explain what users see so they don't worry
- **Operator-focused**: Assume the reader operates a stream, not a developer
- **No internal architecture**: Don't mention `TwitchTokenManager`, `EventSub`, database implementation, etc.
- **Active voice**: "Thiccdal stores a token" not "The token is stored"

### Troubleshooting Section

The troubleshooting section is the most critical part for user satisfaction. Cover:

1. **Popup blocking** — Most common user error; provide browser-specific recovery
2. **Login window doesn't appear** — Redirect to popup blocking or suggest manual URL paste
3. **Auth failed** — Distinguish between user error (clicked Deny) and system error (Twitch down, code expired)
4. **Token refresh failed** — Network issue vs. token revoked (e.g., password changed) vs. stale refresh token
5. **Scope denied** — Explain that some scopes are conditional (e.g., moderator scope); what happens if denied
6. **"No token found"** — Reassure: expected on first run, normal after DB reset
7. **Advanced (console logs)** — Mention that INFO/WARN/ERROR logs are visible in F12 console; suggest screenshots

### When to Document "Gaps"

The task says: "If implementation details are still missing or ambiguous, document only what is actually shipped and call out gaps briefly."

**Example gap call-out** (if applicable):

> **Note**: Thiccdal currently does not support follow event tracking if your bot account is not a moderator of your channel. If you're not a moderator and wish to see follow events, contact your administrator.

This is brief, operator-friendly, and doesn't dive into architecture (EventSub vs. IRC, etc.).

---

## Checklist for Book

Before finalizing a platform connection doc:

- [ ] Quick Start has 5–7 steps, each starting with a verb
- [ ] "What You'll See" covers all three states (before, during, after)
- [ ] Troubleshooting covers at least 6 scenarios
- [ ] Permission scope table has 3 columns: Scope | What It Does | Why
- [ ] No mention of internal classes, database, API calls
- [ ] No "TODO" or architecture terminology
- [ ] Links to related docs (if they exist; use "(if available)" if not)
- [ ] Tone is reassuring and direct, not technical

---

## Example: Twitch (Reference)

See `/docs/help/connecting-to-twitch.md` for a worked example following this pattern.

---

## Reuse When Documenting

Use this skill when adding guides for:
- YouTube connection
- Discord connection
- Kick connection
- Facebook Live connection
- LinkedIn (if API approved)
- TikTok Live (if API approved)

---

## Customizations by Platform

Some platforms may have platform-specific twists:

- **YouTube**: May support device code flow (no browser redirect) — adjust "The Login Flow" section
- **Discord**: May use bot token + OAuth hybrid — clarify which flow is used
- **Kick**: Follow the pattern as-is; unknown at time of writing

Always defer to the actual shipped behavior in the UI components and token manager code. If behavior differs from this pattern, document what's shipped first, then update this skill.
