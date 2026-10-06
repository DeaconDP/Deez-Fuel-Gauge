#Requires -Version 5.1
# Verifies run.ps1 prefers user-local SDK install and never hangs on winget.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
$RunPs1 = Join-Path $RepoRoot 'run.ps1'
if (-not (Test-Path -LiteralPath $RunPs1)) {
    throw "run.ps1 not found at $RunPs1"
}

$text = Get-Content -LiteralPath $RunPs1 -Raw

$checks = @(
    @{ Name = 'uses official dotnet-install.ps1'; Pattern = 'dot\.net/v1/dotnet-install\.ps1'; Invert = $false },
    @{ Name = 'installs into user-profile .dotnet'; Pattern = 'Join-Path \$env:USERPROFILE ''\.dotnet'''; Invert = $false },
    @{ Name = 'discovers user-local dotnet.exe'; Pattern = '\.dotnet\\dotnet\.exe'; Invert = $false },
    @{ Name = 'does not call winget install'; Pattern = 'winget\s+install'; Invert = $true }
)

$failed = New-Object System.Collections.Generic.List[string]
foreach ($check in $checks) {
    $matched = [bool]($text -match $check.Pattern)
    $ok = if ($check.Invert) { -not $matched } else { $matched }
    if (-not $ok) {
        [void]$failed.Add($check.Name)
        Write-Host "FAIL: $($check.Name)" -ForegroundColor Red
    }
    else {
        Write-Host "PASS: $($check.Name)" -ForegroundColor Green
    }
}

# Runtime: with Program Files\dotnet stripped, user-local SDK still works.
$oldPath = $env:Path
try {
    $env:Path = ($env:Path -split ';' | Where-Object { $_ -and ($_ -notmatch '(?i)[\\/]dotnet') }) -join ';'
    Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue

    $userDotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $userDotnet)) {
        Write-Host 'SKIP: runtime discovery (no %USERPROFILE%\.dotnet\dotnet.exe yet)' -ForegroundColor Yellow
    }
    else {
        $sdks = & $userDotnet --list-sdks 2>$null
        $has8 = $false
        foreach ($line in @($sdks)) {
            $version = ($line -split '\s+', 2)[0]
            if ([int]($version.Split('.')[0]) -ge 8) { $has8 = $true; break }
        }
        if (-not $has8) {
            [void]$failed.Add('user-local SDK major >= 8')
            Write-Host 'FAIL: user-local SDK major >= 8' -ForegroundColor Red
        }
        else {
            Write-Host "PASS: user-local SDK usable with PATH stripped ($(& $userDotnet --version))" -ForegroundColor Green
        }
    }
}
finally {
    $env:Path = $oldPath
}

if ($failed.Count -gt 0) {
    throw ("verify-run-ps1-sdk-install failed: " + ($failed -join '; '))
}

Write-Host 'verify-run-ps1-sdk-install: all checks passed.'
