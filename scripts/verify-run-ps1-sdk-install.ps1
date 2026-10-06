#Requires -Version 5.1
# Proves run.ps1 discovers a user-local SDK and installs via dotnet-install.ps1
# (no machine-scope winget hang). Rerun after any run.ps1 install change.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
$RunPs1 = Join-Path $RepoRoot 'run.ps1'
$failures = New-Object System.Collections.Generic.List[string]

function Fail([string]$Message) {
    [void]$failures.Add($Message)
    Write-Host "FAIL: $Message" -ForegroundColor Red
}

function Pass([string]$Message) {
    Write-Host "PASS: $Message" -ForegroundColor Green
}

if (-not (Test-Path -LiteralPath $RunPs1)) {
    throw "run.ps1 not found at $RunPs1"
}

$source = Get-Content -LiteralPath $RunPs1 -Raw

if ($source -notmatch 'dotnet-install\.ps1') {
    Fail 'run.ps1 must install the SDK with official dotnet-install.ps1 (user-local, no admin).'
}
else {
    Pass 'run.ps1 references dotnet-install.ps1'
}

$installIdx = $source.IndexOf('function Install-DotNetSdk')
$wingetIdx = $source.IndexOf('winget install')
$dotnetInstallIdx = $source.IndexOf('dotnet-install.ps1')
if ($installIdx -lt 0) {
    Fail 'Install-DotNetSdk function is missing.'
}
elseif ($dotnetInstallIdx -lt 0) {
    Fail 'dotnet-install.ps1 is not referenced inside run.ps1.'
}
elseif ($wingetIdx -ge 0 -and $wingetIdx -lt $dotnetInstallIdx) {
    Fail 'winget install must not run before user-local dotnet-install.ps1 (machine Burn install hangs on UAC).'
}
else {
    Pass 'Install order prefers user-local dotnet-install.ps1 over winget'
}

if ($source -notmatch '\\\.dotnet\\dotnet\.exe' -and $source -notmatch "\.dotnet['\`"]\\dotnet\.exe" -and $source -notmatch "Join-Path .* '\.dotnet'") {
    Fail 'run.ps1 must look for %USERPROFILE%\.dotnet\dotnet.exe when PATH is stale.'
}
else {
    Pass 'run.ps1 searches the user-local .dotnet install dir'
}

# Runtime: with Program Files\dotnet stripped, user-local SDK must still resolve.
$userDotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
$oldPath = $env:Path
$oldRoot = $env:DOTNET_ROOT
try {
    $env:Path = ($env:Path -split ';' | Where-Object { $_ -and ($_ -notmatch '(?i)[\\/]dotnet') }) -join ';'
    Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue

    if (-not (Test-Path -LiteralPath $userDotnet)) {
        Fail "Expected user-local SDK at $userDotnet for this check. Install once with dotnet-install.ps1 -Channel 8.0 -InstallDir `$HOME\.dotnet"
    }
    else {
        $sdks = & $userDotnet --list-sdks 2>$null
        $has8 = $false
        foreach ($line in @($sdks)) {
            $version = ($line -split '\s+', 2)[0]
            if ([int]($version.Split('.')[0]) -ge 8) { $has8 = $true; break }
        }
        if (-not $has8) {
            Fail "$userDotnet has no SDK major >= 8"
        }
        else {
            Pass "User-local SDK usable without machine PATH ($userDotnet)"
        }
    }

    if (Get-Command winget -ErrorAction SilentlyContinue) {
        # Guardrail: never leave a winget machine install running from this lever.
        $wingetProcs = @(Get-Process -Name winget, 'dotnet-sdk*' -ErrorAction SilentlyContinue)
        if ($wingetProcs.Count -gt 0) {
            Fail ("Unexpected winget/dotnet-sdk process(es) during verify: " + (($wingetProcs | ForEach-Object ProcessName) -join ', '))
        }
        else {
            Pass 'No hung winget/Burn SDK installer processes'
        }
    }

    # End-to-end: run.ps1 must resolve the user-local SDK and build without machine PATH or winget.
    Write-Host '>> Smoke: run.ps1 -SkipLaunch with machine dotnet stripped from PATH'
    $env:Path = ($userDotnet | Split-Path -Parent) + ';' + $env:Path
    $env:DOTNET_ROOT = Split-Path -Parent $userDotnet
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $RunPs1 -SkipLaunch
    if ($LASTEXITCODE -ne 0) {
        Fail "run.ps1 -SkipLaunch failed with exit $LASTEXITCODE under user-local-only PATH"
    }
    else {
        Pass 'run.ps1 -SkipLaunch builds using user-local SDK (no machine PATH)'
    }
}
finally {
    $env:Path = $oldPath
    if ($null -ne $oldRoot -and $oldRoot -ne '') { $env:DOTNET_ROOT = $oldRoot }
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "$($failures.Count) check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host 'All run.ps1 SDK install checks passed.' -ForegroundColor Green
exit 0
