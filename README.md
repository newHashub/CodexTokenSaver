# CodexTokenSaver

CodexTokenSaver is a Windows-first local toolkit for reducing Codex long-session token drag with context packs, handoff summaries, and a tray token warning light.

CodexTokenSaver 是一个 Windows 优先的本地工具包，用 `docs/codex` 上下文包、交接 skill 和托盘红黄绿灯，帮助你在 Codex 长会话变重前及时迁移到新会话。

> Unofficial project. This is not an OpenAI product. It reads local Codex log files and may need updates if Codex changes its local log format.
>
> 非官方项目。它只读取本地 Codex 日志；如果 Codex 未来修改本地日志格式，本项目可能需要适配。

## What It Includes / 包含什么

- **Token Lights**: a Windows tray monitor that reads recent Codex `token_count` events and shows green/yellow/red status.
- **Handoff Summary skill**: say "交接" / "handoff" to generate a compact prompt for a fresh Codex session.
- **Context pack templates**: durable `docs/codex/` files so new sessions can resume from files instead of old chat history.
- **Global AGENTS example**: reusable rules for keeping important project state in files.

## Install / 安装

Requirements:

- Windows 10/11
- Python 3 available as `py -3` or `python`
- Windows .NET Framework compiler, usually available at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`

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

Optional startup shortcut:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -CreateStartupShortcut
```

## Token Lights

The tray icon uses the worst status among recent active Codex sessions:

- Green: latest input tokens below `80k`
- Yellow: `80k` to below `150k`
- Red: `150k+`

Hover shows:

```text
5小时 remaining% reset time
1周   remaining% reset date
light conversation name latest input tokens
```

Right-click opens a compact card with account usage, recent sessions, heavy sessions, refresh controls, and exit.

## Workflow / 推荐工作流

1. Use Codex normally.
2. For important projects, keep durable state in `docs/codex/`.
3. When Token Lights turns yellow, prepare to hand off if the project will continue.
4. When it turns red, say "交接" and start a fresh Codex session with the generated handoff prompt.
5. Archive or close the old session after the new one has resumed safely.

## Privacy / 隐私边界

CodexTokenSaver:

- reads `%USERPROFILE%\.codex\sessions\` and `%USERPROFILE%\.codex\session_index.jsonl`
- does not upload data
- does not edit Codex SQLite databases
- does not rename, archive, or message Codex threads
- does not change Codex account settings

See [docs/privacy.md](docs/privacy.md).

## Repository Layout

```text
apps/token-lights/        Windows tray monitor source
skills/handoff-summary/   installable Codex skill
templates/context-pack/   docs/codex templates
templates/agents/         AGENTS.md example
docs/                     installation, workflow, privacy, troubleshooting
```

## License

MIT
