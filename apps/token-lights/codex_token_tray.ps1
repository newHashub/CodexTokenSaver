$ErrorActionPreference = "SilentlyContinue"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Scanner = Join-Path $BaseDir "codex_token_lights.py"
$Popup = Join-Path $BaseDir "codex_token_popup.exe"
$Settings = Join-Path $BaseDir "settings.json"
$StateJson = Join-Path $BaseDir "tray-state.json"
$IconDir = Join-Path $BaseDir "icons"
$Global:RefreshSeconds = 10
$Global:Rows = @()
$Global:PythonExe = $null
$Global:PythonArgs = @()

function Test-PythonCommand([string]$Exe, [string[]]$Args) {
    try {
        $output = & $Exe @Args -c "import sys; print(sys.executable)" 2>$null
        return -not [string]::IsNullOrWhiteSpace(($output | Select-Object -First 1))
    } catch {
        return $false
    }
}

function Resolve-Python {
    if ($Global:PythonExe) { return }

    $envPython = [Environment]::GetEnvironmentVariable("CODEX_TOKEN_SAVER_PYTHON")
    if (-not [string]::IsNullOrWhiteSpace($envPython) -and (Test-PythonCommand $envPython @())) {
        $Global:PythonExe = $envPython
        $Global:PythonArgs = @()
        return
    }

    if (Test-Path $Settings) {
        try {
            $cfg = Get-Content -Raw -Encoding UTF8 $Settings | ConvertFrom-Json
            $configured = [string]$cfg.python_path
            if (-not [string]::IsNullOrWhiteSpace($configured) -and (Test-PythonCommand $configured @())) {
                $Global:PythonExe = $configured
                $Global:PythonArgs = @()
                return
            }
        } catch {}
    }

    if (Test-PythonCommand "py" @("-3")) {
        $Global:PythonExe = "py"
        $Global:PythonArgs = @("-3")
        return
    }
    if (Test-PythonCommand "python" @()) {
        $Global:PythonExe = "python"
        $Global:PythonArgs = @()
        return
    }
    throw "Python 3 was not found. Install Python or set CODEX_TOKEN_SAVER_PYTHON."
}

function Get-RefreshSeconds {
    if (Test-Path $Settings) {
        try {
            $cfg = Get-Content -Raw -Encoding UTF8 $Settings | ConvertFrom-Json
            $value = [int]$cfg.refresh_seconds
            if ($value -ge 5 -and $value -le 300) { return $value }
        } catch {}
    }
    return 10
}

function Set-RefreshSeconds([int]$Seconds) {
    $Global:RefreshSeconds = $Seconds
    @{ refresh_seconds = $Seconds } | ConvertTo-Json | Set-Content -Encoding UTF8 $Settings
    $Timer.Interval = $Seconds * 1000
}

function Sync-RefreshSeconds {
    $seconds = Get-RefreshSeconds
    if ($seconds -ne $Global:RefreshSeconds) {
        $Global:RefreshSeconds = $seconds
        $Timer.Interval = $seconds * 1000
    }
}

function Get-IconPath([string]$Level) {
    return Join-Path $IconDir "$Level.ico"
}

function Get-WorstLevel($Rows) {
    foreach ($row in $Rows) { if ($row.level -eq "red") { return "red" } }
    foreach ($row in $Rows) { if ($row.level -eq "yellow") { return "yellow" } }
    if ($Rows.Count -gt 0) { return "green" }
    return "gray"
}

function ShortName([string]$Name, [int]$Max = 11) {
    if ([string]::IsNullOrWhiteSpace($Name)) { return "unknown" }
    if ($Name.Length -le $Max) { return $Name }
    return $Name.Substring(0, [Math]::Max(0, $Max - 3)) + "..."
}

function Build-Tooltip($Rows) {
    if ($Rows.Count -eq 0) { return "Codex Token Lights`nNo visible sessions" }
    $RedLamp = [char]::ConvertFromUtf32(0x1F534)
    $YellowLamp = [char]::ConvertFromUtf32(0x1F7E1)
    $GreenLamp = [char]::ConvertFromUtf32(0x1F7E2)
    $UsageLamp = [char]::ConvertFromUtf32(0x26AA)
    $WideSpace = [char]::ConvertFromUtf32(0x3000)
    $Hour5Label = "5" + [char]::ConvertFromUtf32(0x5C0F) + [char]::ConvertFromUtf32(0x65F6)
    $Week1Label = "1" + [char]::ConvertFromUtf32(0x5468) + "  "
    $NameWidth = 10
    $lines = @()
    $latestRows = @($Rows | Sort-Object -Property event_time -Descending)
    $row = $latestRows[0]
    $usageRow = $latestRows[0]
    $lamp = switch ($row.level) {
        "red" { $RedLamp }
        "yellow" { $YellowLamp }
        default { $GreenLamp }
    }
    $name = ShortName $row.name $NameWidth
    $sessionLabel = "$lamp $name"
    $hour5 = "$UsageLamp $Hour5Label"
    $week1 = "$UsageLamp $($Week1Label.Trim())$WideSpace$WideSpace"
    $tokens = ValueOr $row.input_tokens_short
    $window = ValueOr $row.context_window_short
    $primary = ValueOr $usageRow.primary_remaining_short
    $primaryReset = ShortValue (ValueOr $usageRow.primary_reset_short) 5
    $secondary = ValueOr $usageRow.secondary_remaining_short
    $secondaryReset = ShortValue (ValueOr $usageRow.secondary_reset_short) 5
    $lines += "$hour5 $primary $primaryReset"
    $lines += "$week1 $secondary $secondaryReset"
    $lines += "$sessionLabel $tokens $window"
    if ($lines.Count -eq 0) { return "Codex Token Lights" }
    return ($lines -join "`n")
}

function ValueOr($Value, [string]$Fallback = "--") {
    if ($null -eq $Value) { return $Fallback }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return $Fallback }
    return $text
}

function ShortValue([string]$Value, [int]$Max) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "--" }
    if ($Value.Length -le $Max) { return $Value }
    return $Value.Substring(0, $Max)
}

function Refresh-Data {
    try {
        Resolve-Python
        & $Global:PythonExe @Global:PythonArgs $Scanner --json-path $StateJson --limit 20 2>$null | Out-Null
        if (-not (Test-Path $StateJson)) { return }
        $json = Get-Content -Raw -Encoding UTF8 $StateJson
        if ([string]::IsNullOrWhiteSpace($json)) { return }
        $rows = $json | ConvertFrom-Json
        if ($null -eq $rows) { $rows = @() }
        if ($rows -isnot [System.Array]) { $rows = @($rows) }
        $Global:Rows = $rows
        $level = Get-WorstLevel $rows
        $NotifyIcon.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Get-IconPath $level))
        $NotifyIcon.Text = Build-Tooltip $rows
    } catch {
        $NotifyIcon.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Get-IconPath "gray"))
        $NotifyIcon.Text = "Codex Token Lights`nRead error"
    }
}

function Open-Popup {
    if (Test-Path $Popup) {
        Start-Process -FilePath $Popup | Out-Null
    }
}

$Global:RefreshSeconds = Get-RefreshSeconds

$NotifyIcon = New-Object System.Windows.Forms.NotifyIcon
$NotifyIcon.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Get-IconPath "gray"))
$NotifyIcon.Text = "Codex Token Lights"
$NotifyIcon.Visible = $true

$NotifyIcon.ContextMenuStrip = $null
$NotifyIcon.Add_MouseUp({
    param($sender, $eventArgs)
    if ($eventArgs.Button -eq [System.Windows.Forms.MouseButtons]::Right) {
        Open-Popup
    }
})
$Timer = New-Object System.Windows.Forms.Timer
$Timer.Interval = $Global:RefreshSeconds * 1000
$Timer.Add_Tick({
    Sync-RefreshSeconds
    Refresh-Data
})
$Timer.Start()

Refresh-Data
[System.Windows.Forms.Application]::Run()
