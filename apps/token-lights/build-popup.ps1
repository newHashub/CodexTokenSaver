$ErrorActionPreference = "Stop"

$BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Source = Join-Path $BaseDir "codex_token_popup.cs"
$Output = Join-Path $BaseDir "codex_token_popup.exe"

$candidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "C# compiler not found. Install .NET Framework developer tools or compile codex_token_popup.cs manually."
}

& $csc `
    /nologo `
    /target:winexe `
    /optimize+ `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    /out:$Output `
    $Source

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Built $Output"
