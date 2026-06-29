# Global AI Working Rules

## Resource safety hard stop

Do not run commands that can materially slow down, freeze, or crash the user's Windows machine unless the user explicitly approves that specific action first.

Treat these as high-risk by default:

- Broad recursive filesystem searches or scans, especially under `.codex`, `AppData`, browser profiles, `node_modules`, runtimes, caches, or repository roots with unknown size.
- Large SQLite/log inspection without tight bounds, especially `logs_2.sqlite`, WAL files, or large JSONL/session directories.
- Long-running diagnostics, high-frequency watchers, monitors, loops, or refresh jobs.
- Browser profile inspection, full process sweeps, full directory inventory, bulk hashing, indexing, or anything likely to spike CPU, memory, disk I/O, or window count.

Required behavior before any high-risk action:

- Stop and state the risk in plain language: what may spike, why it is needed, and the safer alternative.
- Ask for explicit approval and wait. If approval is not given, do not run it.
- Prefer a bounded alternative: read a known small file, inspect metadata with `Get-Item`, query a fixed small time/id window, cap result counts, exclude heavy directories, or use an existing state/cache file.
- If a high-risk command was started by mistake, stop the process first, then report what ran and what remains running. Do not continue the investigation automatically.

## Ponytail-style minimalism ladder

Before building a tool, writing a script, debugging local state, or running diagnostics, use this ladder:

1. Do we need to do this at all? If not, do nothing.
2. Can public docs, GitHub issues, or existing examples answer it first?
3. Can an existing file, script, state cache, or project tool answer it?
4. Can the standard library, a safe system command, or platform feature solve it?
5. Can an already installed dependency solve it?
6. Can it be solved with a tiny bounded command or a one-shot read?
7. Only then write the smallest implementation that solves the current problem.

For lightweight questions about APIs, flags, fields, tools, scripts, or small utilities, search public web/GitHub sources first. Do not start with broad local searches.

Never use this ladder to justify high-risk local diagnostics. Broad recursive scans, large log/SQLite reads, browser profile inspection, watchers, monitors, or high CPU/memory/disk actions still require explicit user approval first.

## Long-running project context packs

For important projects, long-running projects, recurring workflows, existing long-session cleanup, and business or code work that needs continuity, default to using a Codex context pack. Do not rely only on chat history for important state.

Preferred location inside a repository:

```text
docs/codex/
  context.md
  current-state.md
  decisions.md
  runbook.md
  handoff.md
  archive.md
```

Fallback location when there is no clear repository or project directory:

```text
C:\codex\contexts\<project-name>\
```

At session start, read the context pack before using old chat history. During work, update it when goals, decisions, blockers, verification results, or reusable processes change.

Before replying, check whether the turn produced durable state. If yes, update the relevant context-pack file first.

Do not store secrets, cookies, tokens, passwords, or sensitive account details in context packs.
