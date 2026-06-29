#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$MessageParts,

    [string]$BaseBranch = $env:BASE_BRANCH
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Test-Command([string]$Name) {
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

$Message = ($MessageParts -join ' ').Trim()
if ([string]::IsNullOrWhiteSpace($Message)) {
    Write-Error 'Usage: .\scripts\ship.ps1 "feat: describe the change"'
    exit 1
}

if (-not (Test-Command git)) {
    Write-Error 'git is required.'
    exit 1
}

if (-not (Test-Command gh)) {
    Write-Error 'GitHub CLI is required. Install: winget install GitHub.cli — then run: gh auth login'
    exit 1
}

git rev-parse --is-inside-work-tree 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error 'Run this command inside a Git repository.'
    exit 1
}

$remotes = @(git remote)
if ($remotes.Count -eq 0) {
    Write-Error 'No git remote configured. Add one first, for example: gh repo create --source=. --push'
    exit 1
}

if ([string]::IsNullOrWhiteSpace($BaseBranch)) {
    $BaseBranch = (gh repo view --json defaultBranchRef --jq '.defaultBranchRef.name').Trim()
    if ([string]::IsNullOrWhiteSpace($BaseBranch)) {
        $BaseBranch = 'main'
    }
}

$slug = ($Message.ToLower() -replace '[^a-z0-9]+', '-').Trim('-')
if ($slug.Length -gt 45) {
    $slug = $slug.Substring(0, 45)
}
if ([string]::IsNullOrWhiteSpace($slug)) {
    $slug = 'change'
}

$currentBranch = (git branch --show-current).Trim()

if ($currentBranch -eq $BaseBranch) {
    $branch = "cursor/$slug"
    git show-ref --verify --quiet "refs/heads/$branch" 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $branch = "$branch-$(Get-Date -Format 'yyyyMMddHHmmss')"
    }
    git switch -c $branch
}
else {
    $branch = $currentBranch
}

Write-Host 'Running checks...'

if (Test-Path 'DeezFuelGauge.sln') {
    dotnet restore DeezFuelGauge.sln
    dotnet build DeezFuelGauge.sln -c Release --no-restore
}
else {
    $project = Get-ChildItem -Path . -Filter '*.csproj' -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        Select-Object -First 1
    if ($null -ne $project) {
        dotnet restore $project.FullName
        dotnet build $project.FullName -c Release --no-restore
    }
}

git add --all

git diff --cached --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host 'There are no changes to commit.'
    exit 0
}

git commit -m $Message
git push --set-upstream origin $branch

$prUrl = gh pr create `
    --base $BaseBranch `
    --head $branch `
    --title $Message `
    --fill

Write-Host ''
Write-Host 'Pull request created:'
Write-Host $prUrl
