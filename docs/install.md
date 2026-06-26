# Installation / 安装

## Quick Install

```powershell
git clone https://github.com/newHashub/CodexTokenSaver.git
cd CodexTokenSaver
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

The installer:

- detects Python 3
- writes `apps/token-lights/settings.json`
- compiles `apps/token-lights/codex_token_popup.cs` into a local exe
- copies `skills/handoff-summary/` into `%USERPROFILE%\.codex\skills\handoff-summary`

## Custom Python

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -PythonPath C:\Path\To\python.exe
```

You can also set:

```powershell
$env:CODEX_TOKEN_SAVER_PYTHON = "C:\Path\To\python.exe"
```

## Startup Shortcut

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -CreateStartupShortcut
```
