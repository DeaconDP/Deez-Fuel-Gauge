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

Install the SDK manually, then run this script again:
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
    Join-Path $PSScriptRoot 'CursorUsageWidget\bin\Release\net8.0\CursorUsageWidget.exe'
}

if (-not (Test-DotNetSdk)) {
    Install-DotNetSdk
}

$exePath = Get-BuiltExePath

if (-not (Test-Path $exePath)) {
    Write-Step 'First run: building Cursor Usage Widget...'
    dotnet build CursorUsageWidget.sln -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (exit code $LASTEXITCODE)."
    }
}
else {
    Write-Step 'Rebuilding to pick up any changes...'
    dotnet build CursorUsageWidget.sln -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (exit code $LASTEXITCODE)."
    }
}

if (-not (Test-Path $exePath)) {
    throw "Expected executable was not found: $exePath"
}

Write-Step 'Starting widget...'
Start-Process -FilePath $exePath
