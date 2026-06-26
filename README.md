# CodexTokenSaver

[中文](README.zh-CN.md) | English

CodexTokenSaver is a Windows-first local toolkit that helps Codex users reduce long-session token drag with durable context files, handoff summaries, and a tray token warning light.

> Unofficial project. This is not an OpenAI product. It depends on Codex local log files and may need updates if Codex changes its local log format.

## Why

Long Codex conversations can become slow and expensive to continue because every new turn may carry a large amount of prior context. Image-heavy conversations are especially easy to bloat.

CodexTokenSaver does not shrink an already-heavy conversation in place. Instead, it gives you a practical workflow:

1. Keep durable project state in files.
2. Generate a compact handoff prompt when a session gets heavy.
3. Start a fresh Codex session with only the handoff and relevant files.
4. Use Token Lights to know when a session is becoming expensive to continue.

## What's Included

- **Token Lights**: a Windows tray monitor that reads recent Codex `token_count` events and shows green/yellow/red status.
- **Handoff Summary skill**: an installable Codex skill that creates concise copy-paste prompts for fresh sessions.
- **Context pack templates**: `docs/codex/` templates for durable project memory.
- **AGENTS example**: global rules that tell agents to write important state to files instead of relying on chat history.
- **Install scripts**: source-first setup scripts. No prebuilt executable is committed to the repository.

## Quick Start

Requirements:

- Windows 10/11
- Python 3 available as `py -3` or `python`
- Windows .NET Framework compiler, usually available at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`

Install:

```powershell
git clone https://github.com/newHashub/CodexTokenSaver.git
cd CodexTokenSaver
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

Start Token Lights:

```cmd
apps\token-lights\start.cmd
```

Stop Token Lights:

```cmd
apps\token-lights\stop.cmd
```

Create a Windows startup shortcut:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -CreateStartupShortcut
```

Use a custom Python executable:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -PythonPath C:\Path\To\python.exe
```

## Token Lights

Token Lights reads active Codex session logs from:

```text
%USERPROFILE%\.codex\sessions\
%USERPROFILE%\.codex\session_index.jsonl
```

It uses the latest `last_token_usage.input_tokens` value as the session weight signal:

- Green: below `80k`
- Yellow: `80k` to below `150k`
- Red: `150k+`

The tray icon uses the worst status among recent active sessions. Hover shows:

```text
5小时 remaining% reset time
1周   remaining% reset date
light conversation name latest input tokens
```

Right-click opens a compact control card with:

- remaining 5-hour and 1-week usage
- recent sessions
- heavy sessions
- refresh controls
- exit

Archived sessions are intentionally excluded.

## Handoff Workflow

After installing, the `handoff-summary` skill is copied to:

```text
%USERPROFILE%\.codex\skills\handoff-summary\
```

In Codex, say:

```text
交接
```

or:

```text
Generate a handoff summary for a fresh session.
```

The skill will generate a compact prompt that a new Codex session can use without carrying the full old conversation.

For important projects, it prefers pointing the next session to durable files such as:

```text
docs/codex/context.md
docs/codex/current-state.md
docs/codex/decisions.md
docs/codex/runbook.md
docs/codex/handoff.md
```

## Recommended Workflow

1. Work normally in Codex.
2. For important projects, keep durable state in `docs/codex/`.
3. If Token Lights turns yellow, prepare a handoff if the task will continue.
4. If Token Lights turns red, generate a handoff and continue in a fresh session.
5. Archive or close the old session after the new one resumes safely.

## Privacy and Safety

CodexTokenSaver is local-first.

It does:

- read local Codex JSONL logs
- write runtime files inside `apps/token-lights/`
- copy the handoff skill into your local Codex skills folder

It does not:

- upload data
- modify Codex databases
- rename, archive, or message threads
- change Codex account settings
- send session contents to a server

Generated runtime files such as `tray-state.json` can contain real thread names and token usage data. They are ignored by git and should not be published.

See [docs/privacy.md](docs/privacy.md).

## Repository Layout

```text
apps/token-lights/        Windows tray monitor source
skills/handoff-summary/   installable Codex skill
templates/context-pack/   docs/codex templates
templates/agents/         AGENTS.md example
docs/                     installation, workflow, privacy, troubleshooting
```

## Development Checks

```powershell
python -m py_compile apps\token-lights\codex_token_lights.py
powershell -NoProfile -ExecutionPolicy Bypass -File apps\token-lights\build-popup.ps1
```

## Limitations

- Windows-first.
- Depends on Codex local JSONL log shape.
- Token Lights is a warning signal, not a precise billing dashboard.
- It does not automate session creation, archival, or account actions.

## License

MIT
