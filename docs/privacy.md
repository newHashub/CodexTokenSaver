# Privacy and Safety / 隐私与安全

CodexTokenSaver is local-first.

It reads:

- `%USERPROFILE%\.codex\sessions\`
- `%USERPROFILE%\.codex\session_index.jsonl`

It writes only inside its own app folder by default:

- `apps/token-lights/settings.json`
- `apps/token-lights/tray-state.json`
- `apps/token-lights/codex_token_popup.exe`

It does not:

- upload data
- modify Codex databases
- change Codex account settings
- rename, archive, or message threads
- send session contents to a server

Do not publish generated runtime files such as `tray-state.json`; they can contain real thread names and token usage data.
