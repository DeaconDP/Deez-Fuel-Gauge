param(
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $Root "CursorUsageWidget"
$Csproj = Join-Path $ProjectDir "CursorUsageWidget.csproj"
$PublishDir = Join-Path $Root "publish"
$ReleasesDir = Join-Path $Root "Releases"

if (-not $Version) {
    [xml]$csprojXml = Get-Content $Csproj
    $Version = $csprojXml.Project.PropertyGroup.Version
    if (-not $Version) {
        throw "Version not found in $Csproj and -Version was not provided."
    }
}

Write-Host "Building cursor-usage-widget v$Version"

Push-Location $Root
try {
    if (Test-Path $PublishDir) {
        Remove-Item -Recurse -Force $PublishDir
    }

    dotnet publish $Csproj -c $Configuration -r $Runtime --self-contained -o $PublishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not (Test-Path $ReleasesDir)) {
        New-Item -ItemType Directory -Path $ReleasesDir | Out-Null
    } else {
        Get-ChildItem $ReleasesDir -File | Remove-Item -Force
    }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet tool run vpk -- pack `
        --packId cursor-usage-widget `
        --packVersion $Version `
        --packDir $PublishDir `
        --mainExe CursorUsageWidget.exe `
        --outputDir $ReleasesDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    Write-Host "Release artifacts in $ReleasesDir"
    Get-ChildItem $ReleasesDir | ForEach-Object { Write-Host "  $($_.Name)" }
}
finally {
    Pop-Location
}
