---
name: "ai-chatter-memory-guardrails"
description: "Design AI chatter memory as a short-lived, platform-scoped, sanitized summary instead of a raw conversational dossier."
domain: "security"
confidence: "high"
source: "earned"
---

## Context

Use this when Thiccdal adds personalization or continuity to AI chat replies. The feature sounds harmless, but it changes privacy expectations, increases prompt-injection replay risk, and can create moderation issues if the bot remembers too much or remembers it in the wrong place.

## Patterns

- Keep chatter memory **derived, tiny, and expiring**; do not replay raw history.
- Scope memory by platform-owned identity, not display name alone and not cross-platform joins.
- Only use **public chat** as source material, and only for public-chat continuity.
- Inject a sanitized summary into the prompt, never `RawData`, HTML, secrets, or moderation notes.
- Treat memory writes as application-controlled logic; the model must not author its own long-term memory.

## Examples

- `src/Modules/Thiccdal.Modules.ChatBot/Services/ChatBotAiResponder.cs` currently uses only the active chat event in prompt construction; a future memory block should stay similarly minimal.
- `src/Thiccdal.Data/ChatPersistenceService.cs` and `src/Thiccdal.Data/Models/ChatMessage.cs` already persist raw chat payloads, which is why AI memory must avoid feeding those fields back into prompts.
- `src/Thiccdal.Data/Models/PlatformUser.cs` shows the safe scoping anchor: per-platform user identity.

## Anti-Patterns

- Dumping "last 20 messages" into the AI prompt for personalization.
- Storing forever because the chat was "public anyway."
- Joining a Twitch chatter to Discord/YouTube/X by guessed username similarity.
- Saving sensitive traits, moderation labels, or prompt-injection strings as memory.
- Letting the bot say creepy recall lines that users never expected from a stream chat assistant.
