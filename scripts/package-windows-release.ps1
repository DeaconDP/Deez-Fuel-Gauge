#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ReleasesDir = Join-Path $RepoRoot 'Releases'
$Project = Join-Path $RepoRoot 'DeezFuelGauge\DeezFuelGauge.csproj'
$PublishDir = Join-Path $RepoRoot '.publish\win-x64'
$ZipName = 'deez-fuel-gauge-win-Portable.zip'
$ZipPath = Join-Path $ReleasesDir $ZipName

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error '.NET 8 SDK not found.'
    exit 1
}

New-Item -ItemType Directory -Force -Path $ReleasesDir | Out-Null
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}

dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir `
    --nologo

if (-not (Test-Path (Join-Path $PublishDir 'DeezFuelGauge.exe'))) {
    Write-Error 'Published executable was not created.'
    exit 1
}

if (Test-Path $ZipPath) {
    Remove-Item -Force $ZipPath
}

Compress-Archive -Path (Join-Path $PublishDir '*') -DestinationPath $ZipPath -Force
Write-Output $ZipPath
