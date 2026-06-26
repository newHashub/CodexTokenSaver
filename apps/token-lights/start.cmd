@echo off
setlocal
cd /d "%~dp0"

if not exist "codex_token_popup.exe" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-popup.ps1"
  if errorlevel 1 exit /b %errorlevel%
)

wscript.exe "%~dp0start_hidden.vbs"
