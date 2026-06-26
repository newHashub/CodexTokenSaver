# Global AI Working Rules

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
