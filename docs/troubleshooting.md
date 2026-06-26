# Troubleshooting / 故障排查

## Python was not found

Install Python 3 or rerun:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -PythonPath C:\Path\To\python.exe
```

## Popup exe was not built

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\apps\token-lights\build-popup.ps1
```

If the C# compiler is missing, install .NET Framework developer tools or compile `codex_token_popup.cs` manually with references to:

- `System.Windows.Forms.dll`
- `System.Drawing.dll`
- `System.Web.Extensions.dll`

## No token data appears

Check that Codex has local logs under:

```text
%USERPROFILE%\.codex\sessions\
```

Then run:

```cmd
python apps\token-lights\codex_token_lights.py --once
```

## Tray icon is gray

Gray means no readable data yet or a read error. Right-click the tray icon, refresh, and check whether Codex has written recent `token_count` events.
