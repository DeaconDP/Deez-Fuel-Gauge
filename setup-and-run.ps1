#Requires -Version 5.1
[CmdletBinding()]
param(
    # Set when this script relaunches itself after a self-update so the
    # updated copy does not try to update again.
    [switch]$SkipUpdate
)

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

function Update-RepoToLatest {
    # Brings this checkout to the latest version: fast-forwards the current
    # branch, and if the branch no longer exists on GitHub (it was merged),
    # hops back to the default branch so setup never keeps rebuilding stale
    # code. Returns $true only when HEAD moved, so the caller knows to
    # relaunch with the updated scripts. Any failure here is non-fatal:
    # setup continues with the version already on disk.
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Step 'git is not installed; skipping the update check.'
        return $false
    }

    if (-not (Test-Path (Join-Path $PSScriptRoot '.git'))) {
        Write-Step 'This folder is not a git checkout; skipping the update check.'
        return $false
    }

    # git writes routine progress to stderr; under $ErrorActionPreference =
    # 'Stop', Windows PowerShell 5.1 would turn that into a terminating error
    # whenever the stream is redirected. Relax it while git runs.
    $ErrorActionPreference = 'Continue'
    try {
        $branch = git rev-parse --abbrev-ref HEAD
        if ($branch -eq 'HEAD') {
            Write-Step 'Detached checkout; skipping the update check.'
            return $false
        }

        Write-Step 'Checking GitHub for the latest version...'
        # Out-Host keeps git's output on the console without leaking it into
        # this function's return value.
        git fetch --prune origin | Out-Host
        if ($LASTEXITCODE -ne 0) {
            Write-Warning 'Could not reach GitHub (offline?). Continuing with the current version.'
            return $false
        }

        $before = git rev-parse HEAD

        $default = (git rev-parse --abbrev-ref origin/HEAD 2> $null) -replace '^origin/', ''
        if ($LASTEXITCODE -ne 0 -or -not $default) { $default = 'main' }

        # Branch matching assumes the cursor/Claude workflow: local branches
        # share their name with the GitHub branch they came from.
        git rev-parse --verify --quiet "origin/$branch" *> $null
        $existsOnGitHub = ($LASTEXITCODE -eq 0)
        git rev-parse --verify --quiet "origin/$default" *> $null
        $defaultExists = ($LASTEXITCODE -eq 0)

        if (-not $existsOnGitHub -and $defaultExists -and $branch -ne $default) {
            if (git status --porcelain) {
                Write-Step "Branch '$branch' is not on GitHub and has local edits; running that work-in-progress."
            }
            elseif (git log --oneline "origin/$default..$branch") {
                Write-Step "Branch '$branch' has commits that are not on '$default' yet; running that work-in-progress."
            }
            else {
                Write-Step "Branch '$branch' is fully merged; switching to '$default'..."
                git checkout $default | Out-Host
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "Could not switch to '$default'. Continuing with the current version."
                    return $false
                }
                $branch = $default
                $existsOnGitHub = $true
            }
        }

        if ($existsOnGitHub) {
            git merge --ff-only "origin/$branch" | Out-Host
            if ($LASTEXITCODE -ne 0) {
                Write-Warning 'Could not fast-forward (local changes in the way, or the branch has diverged). Continuing with the current version.'
                return $false
            }
        }

        $after = git rev-parse HEAD
        return ($after -ne $before)
    }
    finally {
        $ErrorActionPreference = 'Stop'
    }
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

    throw 'The widget is still running and is locking the executable. Close it manually, then run setup-and-run again.'
}

try {
    if (-not $SkipUpdate -and (Update-RepoToLatest)) {
        # New commits may include changes to this very script, so hand off to
        # the freshly pulled copy instead of continuing with the stale one.
        Write-Step 'Updated to the latest version; restarting setup with the new scripts...'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -SkipUpdate
        exit $LASTEXITCODE
    }

    if (-not (Test-DotNetSdk)) {
        Install-DotNetSdk
    }

    Stop-RunningWidget

    Write-Step 'Building Deez Fuel Gauge...'
    dotnet build DeezFuelGauge.sln -c Release --nologo -v q
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
    Write-Host "Setup failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ''
    Read-Host 'Press Enter to close this window'
    exit 1
}
