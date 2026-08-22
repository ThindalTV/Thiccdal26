---
name: docs-style
description: Microsoft Style Guide conventions for writing or editing documentation under docs/ and architecture/. Use when creating or revising end-user help pages, architecture docs, or ADRs in this repo.
---

# Documentation style

Docs in `docs/help/`, `docs/architecture/`, and `architecture/` follow the Microsoft Style Guide.
Consistency matters more than any individual preference here.

## Rules

- **Sentence-case headings** — "Getting started", not "Getting Started".
- **Active voice** — "Run the command", not "The command should be run".
- **Second person** — "You can configure…", not "Users can configure…".
- **Present tense** — "The system routes…", not "The system will route…".
- **No ampersands in prose** — write "and". Exceptions: code, brand names, literal UI labels.

## Structure

Lead with what the reader does, not with background.

```
# Page title

> Callout, if the feature is experimental or has a prerequisite

Short statement of what this page gets you.

## First task heading
...

## Related
- Links to neighbouring pages
```

- Narrative paragraphs: three or four sentences, maximum.
- Bullets for anything scannable; tables for structured comparisons.
- Cross-references go at the bottom, after the main content.

## For end-user help pages

`docs/help/` is written for a streamer setting the system up, not for a developer. Name the actual
UI elements they will click. Where a setting has moved from a config file into on-site settings,
document the on-site path — the config file is an implementation detail.

## Anti-patterns

- Title-casing headings because it looks tidier.
- Passive voice or third person.
- Long dense paragraphs that defeat scanning.
- Documenting the code path instead of the user's task.
- Letting a doc drift from the code without saying it is stale — if you notice a mismatch and
  cannot fix it, note it explicitly rather than leaving it silently wrong.
