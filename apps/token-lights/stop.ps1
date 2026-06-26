$ErrorActionPreference = "SilentlyContinue"

$BaseDir = (Split-Path -Parent $MyInvocation.MyCommand.Path).TrimEnd("\")
$escapedBase = [regex]::Escape($BaseDir)

Get-CimInstance Win32_Process | Where-Object {
    $cmd = $_.CommandLine
    if ([string]::IsNullOrWhiteSpace($cmd)) { return $false }
    $inThisApp = $cmd -match $escapedBase
    $isTokenSaver =
        ($_.Name -eq "codex_token_popup.exe") -or
        ($_.Name -in @("python.exe", "pythonw.exe", "py.exe") -and $cmd -like "*codex_token_lights.py*") -or
        ($_.Name -eq "powershell.exe" -and $cmd -like "*codex_token_tray.ps1*")
    return $inThisApp -and $isTokenSaver
} | ForEach-Object {
    Stop-Process -Id $_.ProcessId
}
