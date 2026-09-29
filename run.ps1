#Requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest
Set-Location $PSScriptRoot

function Write-Step([string]$Message) {
    Write-Host ">> $Message" -ForegroundColor Cyan
}

function Refresh-Path {
    $machine = [System.Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user = [System.Environment]::GetEnvironmentVariable('Path', 'User')
    $userDotnet = Join-Path $env:USERPROFILE '.dotnet'
    $parts = @($userDotnet, $machine, $user) | Where-Object { $_ }
    $env:Path = ($parts -join ';')
}

function Get-VersionLabel {
    # e.g. " (main @ 1af526a)" — empty when git is unavailable.
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { return '' }
    if (-not (Test-Path (Join-Path $PSScriptRoot '.git'))) { return '' }
    $ErrorActionPreference = 'Continue'
    try {
        $branch = git rev-parse --abbrev-ref HEAD
        $sha = git rev-parse --short HEAD
        if ($LASTEXITCODE -eq 0) { return " ($branch @ $sha)" }
        return ''
    }
    finally {
        $ErrorActionPreference = 'Stop'
    }
}

function Get-DotNetCandidates {
    $candidates = New-Object System.Collections.Generic.List[string]

    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        [void]$candidates.Add($cmd.Source)
    }

    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $known = @(
        (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'),
        (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe')
    )
    if ($programFilesX86) {
        $known += (Join-Path $programFilesX86 'dotnet\dotnet.exe')
    }

    foreach ($path in $known) {
        if ($path -and (Test-Path -LiteralPath $path)) {
            [void]$candidates.Add($path)
        }
    }

    $candidates | Select-Object -Unique
}

function Test-DotNetSdkAt([string]$DotNetExe) {
    if (-not $DotNetExe -or -not (Test-Path -LiteralPath $DotNetExe)) {
        return $false
    }

    $sdks = & $DotNetExe --list-sdks 2>$null
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

function Find-DotNetSdk {
    foreach ($exe in Get-DotNetCandidates) {
        if (Test-DotNetSdkAt $exe) {
            return $exe
        }
    }

    return $null
}

function Use-DotNet([string]$DotNetExe) {
    $dir = Split-Path -Parent $DotNetExe
    $env:DOTNET_ROOT = $dir
    if ($env:Path -notlike "$dir;*") {
        $env:Path = $dir + ';' + $env:Path
    }
}

function Open-DotNetDownloadPage {
    Start-Process 'https://dotnet.microsoft.com/download/dotnet/8.0' -ErrorAction SilentlyContinue | Out-Null
}

function Install-DotNetSdkUserLocal {
    $installDir = Join-Path $env:USERPROFILE '.dotnet'
    $scriptPath = Join-Path $env:TEMP ("dotnet-install-{0}.ps1" -f [guid]::NewGuid().ToString('N'))

    Write-Step 'Installing .NET 8 SDK to your user profile (no admin)...'
    $ProgressPreference = 'SilentlyContinue'
    try {
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $scriptPath -UseBasicParsing
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
            -Channel 8.0 -InstallDir $installDir -NoPath
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet-install.ps1 exited with code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $scriptPath -Force -ErrorAction SilentlyContinue
    }

    $dotnet = Join-Path $installDir 'dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnet)) {
        throw "dotnet-install.ps1 finished, but $dotnet was not created."
    }

    Use-DotNet $dotnet
    if (-not (Test-DotNetSdkAt $dotnet)) {
        throw 'User-local SDK install finished, but no .NET 8+ SDK was detected.'
    }

    return $dotnet
}

function Install-DotNetSdk {
    try {
        return Install-DotNetSdkUserLocal
    }
    catch {
        Open-DotNetDownloadPage
        throw @"
.NET 8 SDK (or newer) is required and automatic user-local install failed:
$($_.Exception.Message)

Install the SDK from the page that opened, then double-click run.bat again:
https://dotnet.microsoft.com/download/dotnet/8.0
"@
    }
}

function Get-BuiltExePath {
    Join-Path $PSScriptRoot 'DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe'
}

function Stop-RunningWidget {
    # Also stop the pre-rename app so a stale copy can't stay on screen and
    # masquerade as the build we are about to launch.
    $names = 'DeezFuelGauge', 'CursorUsageWidget'
    $processes = @(Get-Process -Name $names -ErrorAction SilentlyContinue)
    if ($processes.Count -eq 0) {
        return
    }

    Write-Step 'Stopping running widget so the build can replace the executable...'
    foreach ($process in $processes) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = @(Get-Process -Name $names -ErrorAction SilentlyContinue)
        if ($remaining.Count -eq 0) {
            return
        }
        Start-Sleep -Milliseconds 100
    }

    throw 'The widget is still running and is locking the executable. Close it manually, then run run.bat again.'
}

try {
    Refresh-Path
    $dotnet = Find-DotNetSdk
    if (-not $dotnet) {
        $dotnet = Install-DotNetSdk
    }
    Use-DotNet $dotnet

    Stop-RunningWidget

    Write-Step 'Building Deez Fuel Gauge...'
    & $dotnet build DeezFuelGauge.sln -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (exit code $LASTEXITCODE)."
    }

    $exePath = Get-BuiltExePath
    if (-not (Test-Path $exePath)) {
        throw "Expected executable was not found: $exePath"
    }

    Write-Step "Starting Deez Fuel Gauge$(Get-VersionLabel) from $exePath"
    Start-Process -FilePath $exePath
}
catch {
    Write-Host ''
    Write-Host "Run failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ''
    Read-Host 'Press Enter to close this window'
    exit 1
}
