---
name: "ai-chatter-memory-seam"
description: "Add safe per-chatter AI memory by deriving bounded context from persisted chat through an Infrastructure seam."
domain: "ai-architecture"
confidence: "high"
source: "earned"
---

## Context

Use this when Thiccdal needs the AI responder to remember a viewer across messages or sessions without leaking data across platforms or bloating prompts.

## Patterns

- Keep memory outside the model client; build it in the chatbot/application layer before `IChatCompletionClient`.
- Define a repo-owned seam such as `IChatterMemoryService` in Infrastructure and implement it in `Thiccdal.Data`.
- Treat persisted `ChatMessages` and `PlatformUsers` as the source of truth for memory.
- Scope memory by `{PlatformEventSource, Channel, PlatformUserId}`, never by display name alone.
- Start with a short bounded summary or recent-facts view; add a derived summary table only if prompt size or latency forces it.
- Keep `IChatCompletionClient` unchanged unless multiple AI features truly need a shared prompt-building abstraction.

## Examples

- `src\Modules\Thiccdal.Modules.ChatBot\Services\ChatBotAiResponder.cs`
- `src\Thiccdal.Infrastructure\AI\IChatCompletionClient.cs`
- `src\Thiccdal.Data\ChatPersistenceService.cs`
- `src\Thiccdal.Data\Models\ChatMessage.cs`
- `src\Thiccdal.Data\Models\PlatformUser.cs`
- `src\Thiccdal.Data\PlatformUserIdResolver.cs`
- `src\Thiccdal.Infrastructure\Bot\Models\PlatformEvent.cs`

## Anti-Patterns

- Sending full raw chat history to the model on every reply.
- Using vendor-managed memory or an external vector store for the first implementation.
- Merging viewer identities across Twitch/YouTube/Discord by matching display names.
- Hiding persistence inside the remote adapters or inside `IChatCompletionClient`.
