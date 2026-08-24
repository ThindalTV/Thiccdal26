---
name: windows-dev
description: Windows and PowerShell gotchas for this repo — path handling, git invocation, multi-line commit messages, and stray artifacts/ build output. Use when running shell commands, scripting, or committing on a Windows dev machine.
---

# Windows development gotchas

The primary dev machine for this repo is Windows. These are failure modes that have actually cost
time here.

## Filenames and timestamps

- **Never put a colon in a filename.** Raw ISO 8601 (`2026-03-15T05:30:00Z`) is illegal on
  Windows. Use `2026-03-15T05-30-00Z` instead.
- Don't inline `.toISOString().replace(/:/g, '-')` at each call site — centralise it.

## Git

- **Avoid `git -C {path}`.** It is unreliable with Windows paths (backslashes, spaces, drive
  letters). `cd` into the directory first, then run git.
- **Never embed `\n` in `git commit -m`.** It fails silently in PowerShell. Use a heredoc via
  the Bash tool, or write the message to a temp file and use `git commit -F`.
- Check there is something to commit before committing: `git diff --cached --quiet`
  (exit 0 means no staged changes).

## Paths

- Don't assume the working directory is the repo root. Resolve it with
  `git rev-parse --show-toplevel`.
- Build paths with `Path.Combine` / `path.join`, never by concatenating `/` or `\`.

## Shell choice

Both PowerShell and Git Bash are available and take different syntax. PowerShell has no
`head`/`tail`/`which`/`touch`; Bash has no `Get-ChildItem`. Pick one per command and stay in it.
`sed`/`awk` in Git Bash mangle Windows path backslashes in replacement strings — prefer a
heredoc or a proper editor for anything with `\` in it.

## Stray build output

`dotnet build --artifacts-path` (or an interrupted build) can leave `artifacts/` folders scattered
through `src/`. They are gitignored, but MSBuild still globbed the generated `AssemblyInfo.cs`
inside them into the compile, producing `CS0579: Duplicate 'AssemblyConfigurationAttribute'`.
`Directory.Build.props` now sets `DefaultItemExcludes` to exclude `**\artifacts\**`. If you see
duplicate-attribute errors, look for stray `artifacts/` and `obj/` directories first.
