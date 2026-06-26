param(
    [string]$CodexHome = (Join-Path $env:USERPROFILE ".codex"),
    [string]$PythonPath = "",
    [switch]$CreateStartupShortcut
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppDir = Join-Path $RepoRoot "apps\token-lights"
$SkillSource = Join-Path $RepoRoot "skills\handoff-summary"
$SkillTarget = Join-Path $CodexHome "skills\handoff-summary"
$Settings = Join-Path $AppDir "settings.json"

function Test-Python([string]$Exe, [string[]]$Args) {
    try {
        $output = & $Exe @Args -c "import sys; print(sys.executable)" 2>$null
        $first = $output | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($first)) { return [string]$first }
    } catch {}
    return ""
}

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $PythonPath = Test-Python "py" @("-3")
}
if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $PythonPath = Test-Python "python" @()
}
if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    throw "Python 3 was not found. Install Python 3, or rerun install.ps1 -PythonPath C:\Path\To\python.exe"
}

New-Item -ItemType Directory -Force -Path $SkillTarget | Out-Null
Copy-Item -Path (Join-Path $SkillSource "*") -Destination $SkillTarget -Recurse -Force

@{
    refresh_seconds = 10
    python_path = $PythonPath
} | ConvertTo-Json | Set-Content -LiteralPath $Settings -Encoding UTF8

powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $AppDir "build-popup.ps1")

if ($CreateStartupShortcut) {
    $startupDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
    $shortcutPath = Join-Path $startupDir "CodexTokenSaver.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $AppDir "start_hidden.vbs"
    $shortcut.WorkingDirectory = $AppDir
    $shortcut.Description = "Start CodexTokenSaver Token Lights"
    $shortcut.Save()
}

Write-Host "CodexTokenSaver installed."
Write-Host "Skill copied to: $SkillTarget"
Write-Host "Start Token Lights: $AppDir\start.cmd"
