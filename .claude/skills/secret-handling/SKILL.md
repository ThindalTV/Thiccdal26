---
name: secret-handling
description: Rules for handling credentials in this repo — which files never to read, what must never be written into committed files or docs, and what to do if a secret reaches git history. Use when touching configuration, platform credentials, connection strings, or the settings store.
---

# Secret handling

This project holds live streaming credentials: Twitch client secrets, Discord bot tokens,
YouTube/LinkedIn/TikTok/Facebook access tokens, RTMP stream keys, and an RTMP server API key.
Several of these grant broadcast rights to a real channel. Treat them accordingly.

## Never read

Do not open files that hold live credentials just to understand the shape of the config:

- `appsettings.Development.json` and any local, gitignored `appsettings.*.json` override
- `.env` and any `.env.*` variant
- User-secrets stores (`secrets.json` under the UserSecretsId)
- `thiccdal.db` (the SQLite file holds persisted platform tokens)

Instead, read the committed `src/Thiccdal/appsettings.json`, which carries the same keys with
empty placeholder values, or the `*Options.cs` class that defines the shape — or just ask.

Reading a config *schema* is fine. Reading live *values* is not.

## Never write

Do not put real values into anything that gets committed — source, docs, test fixtures, commit
messages, or PR descriptions. Watch for:

| Kind | Looks like |
|---|---|
| API keys / tokens | `ClientSecret=…`, `ghp_…`, `sk-…` |
| Connection strings with credentials | `Server=…;Password=…` |
| JWTs | `eyJ….eyJ….…` |
| Private keys | `-----BEGIN … PRIVATE KEY-----` |
| RTMP stream keys | the tail segment of an ingest URL |

Write a reference instead of a value: "set `Twitch:ClientSecret` via user secrets", or
"stream key is configured in the restream settings", never the value itself.

Test fixtures use obvious fakes (`"test-client-secret"`), never a redacted real credential.

## Configuration is moving to the database

Settings are migrating from `appsettings.json` to the `AppConfiguration` table via
`IConfigurationPersistenceService`. That store holds secrets in the SQLite file, so:

- Do not log values read from it, and do not echo them into operator UI beyond a masked field.
- Do not dump the table in diagnostics, error messages, or support output.
- The database file is not a safe place to commit — keep `thiccdal.db` gitignored.

## If a secret reaches git history

1. Stop. Do not make further commits.
2. Tell the user immediately: which commit, which file, which credential.
3. The credential must be **revoked and rotated first** — assume it is compromised the moment it
   is pushed. Scrubbing history does not undo exposure.
4. History rewriting (`git filter-repo`, BFG) and force-pushing is the user's call, not something
   to do unprompted.
