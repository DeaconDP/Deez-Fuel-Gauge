# Programmatic expand/collapse proof. Rerun after a Release build.
$ErrorActionPreference = "Stop"
$exe = Join-Path (Split-Path $PSScriptRoot -Parent) "DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe"
if (-not (Test-Path $exe)) { throw "Missing $exe. Build Release first." }

Get-Process -Name DeezFuelGauge -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
Remove-Item "$env:TEMP\deez-compact-selftest.txt" -ErrorAction SilentlyContinue

$env:DEEZ_COMPACT_SELFTEST = "1"
$env:DEEZ_COMPACT_TRACE = "1"
$proc = Start-Process -FilePath $exe -PassThru
$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not $proc.HasExited -and $sw.Elapsed.TotalSeconds -lt 30) {
    Start-Sleep -Milliseconds 300
}
if (-not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force
    throw "Self-test timed out."
}

$path = Join-Path $env:TEMP "deez-compact-selftest.txt"
if (-not (Test-Path $path)) { throw "Missing self-test report at $path" }
$line = Get-Content $path -Raw
Write-Host $line.Trim()
if ($line -notmatch "pass=True") { exit 1 }
Write-Host "PASS: compact self-test"
exit 0
