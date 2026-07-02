#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest
Set-Location $PSScriptRoot

function Write-Step([string]$Message) {
    Write-Host ">> $Message" -ForegroundColor Cyan
}

function Refresh-Path {
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
                [System.Environment]::GetEnvironmentVariable('Path', 'User')
}

function Test-DotNetSdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return $false
    }

    $sdks = dotnet --list-sdks 2>$null
    if (-not $sdks) {
        return $false
    }

    foreach ($line in $sdks) {
        $version = ($line -split '\s+', 2)[0]
        $major = [int]($version.Split('.')[0])
        if ($major -ge 8) {
            return $true
        }
    }

    return $false
}

function Install-DotNetSdk {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw @"
.NET 8 SDK (or newer) is required but was not found, and winget is not available.

Install the SDK from the page that opens, then double-click setup-and-run.bat again:
https://dotnet.microsoft.com/download/dotnet/8.0
"@
    }

    Write-Step 'Installing .NET 8 SDK via winget (one-time)...'
    winget install --id Microsoft.DotNet.SDK.8 -e `
        --accept-package-agreements --accept-source-agreements

    Refresh-Path

    if (-not (Test-DotNetSdk)) {
        throw 'SDK install finished, but dotnet is still not on PATH. Close this window, open a new one, and double-click setup-and-run.bat again.'
    }
}

function Get-BuiltExePath {
    Join-Path $PSScriptRoot 'DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe'
}

function Stop-RunningWidget {
    $processes = @(Get-Process -Name 'DeezFuelGauge' -ErrorAction SilentlyContinue)
    if ($processes.Count -eq 0) {
        return
    }

    Write-Step 'Stopping running widget so the build can replace the executable...'
    foreach ($process in $processes) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = @(Get-Process -Name 'DeezFuelGauge' -ErrorAction SilentlyContinue)
        if ($remaining.Count -eq 0) {
            return
        }
        Start-Sleep -Milliseconds 100
    }

    throw 'DeezFuelGauge is still running and is locking the executable. Close it manually, then run setup-and-run again.'
}

if (-not (Test-DotNetSdk)) {
    Install-DotNetSdk
}

$exePath = Get-BuiltExePath

Stop-RunningWidget

if (-not (Test-Path $exePath)) {
    Write-Step 'First run: building Deez Fuel Gauge...'
    dotnet build DeezFuelGauge.sln -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (exit code $LASTEXITCODE)."
    }
}
else {
    Write-Step 'Rebuilding to pick up any changes...'
    dotnet build DeezFuelGauge.sln -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (exit code $LASTEXITCODE)."
    }
}

if (-not (Test-Path $exePath)) {
    throw "Expected executable was not found: $exePath"
}

Write-Step 'Starting widget...'
Start-Process -FilePath $exePath
