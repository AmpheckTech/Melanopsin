# Publishes the prepared wiki pages (see prepare-wiki.ps1) to the GitHub wiki.
#
# Requires WIKI_TOKEN: a personal access token that can write to <owner>/<repo>.wiki.git.
# The GITHUB_TOKEN of a workflow is scoped to the repository it runs in and is not
# accepted by the wiki remote, so a classic PAT with the "repo" scope (or an equivalent
# fine-grained token) has to be stored as the WIKI_TOKEN secret. Without the secret the
# script logs a warning and succeeds, so forks and pull requests do not fail.
#
# The wiki needs at least one page (create the Home page in the browser once) before its
# git repository can be cloned.
#
# Usage:
#     $env:WIKI_TOKEN = '...'; pwsh -File docs/api/publish-wiki.ps1

[CmdletBinding()]
param(
    [string]$SourceRoot = 'artifacts/wiki',
    [string]$WorkingDirectory = 'artifacts/wiki-repo',
    [string]$Repository = '',
    [string]$CommitMessage = ''
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Resolve-RepositoryPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
}

$sourceRoot = Resolve-RepositoryPath $SourceRoot
$cloneRoot = Resolve-RepositoryPath $WorkingDirectory

$token = $env:WIKI_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host '::warning::WIKI_TOKEN is not configured, skipping the wiki publication. Add a personal access token with wiki write access as the WIKI_TOKEN secret.'
    return
}

if ([string]::IsNullOrWhiteSpace($Repository)) {
    $Repository = $env:GITHUB_REPOSITORY
}

if ([string]::IsNullOrWhiteSpace($Repository)) {
    throw 'Could not determine the GitHub repository; pass -Repository <owner>/<name>.'
}

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Nothing to publish: $sourceRoot does not exist. Run docs/api/prepare-wiki.ps1 first."
}

if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
    $sha = $env:GITHUB_SHA
    $CommitMessage = if ([string]::IsNullOrWhiteSpace($sha)) {
        'Update API reference'
    } else {
        'Update API reference from {0}' -f $sha.Substring(0, 7)
    }
}

$remoteUrl = "https://github.com/$Repository.wiki.git"
$authenticatedUrl = "https://x-access-token:$token@github.com/$Repository.wiki.git"

if (Test-Path -LiteralPath $cloneRoot) {
    Remove-Item -LiteralPath $cloneRoot -Recurse -Force
}

Write-Host "Cloning $remoteUrl..."
git clone --quiet $authenticatedUrl $cloneRoot
if ($LASTEXITCODE -ne 0) {
    throw "Could not clone $remoteUrl. Check that the wiki has a Home page and that WIKI_TOKEN can write to it."
}

Push-Location $cloneRoot
try {
    # Generated pages are named "Melanopsin.*"; the navigation files are owned by
    # prepare-wiki.ps1. Anything else (Home.md, hand written pages) is preserved.
    Get-ChildItem -LiteralPath $cloneRoot -File -Filter 'Melanopsin.*.md' | Remove-Item -Force
    foreach ($managed in @('API-Reference.md', '_Sidebar.md', '_Footer.md')) {
        $managedPath = Join-Path $cloneRoot $managed
        if (Test-Path -LiteralPath $managedPath) {
            Remove-Item -LiteralPath $managedPath -Force
        }
    }

    Copy-Item -Path (Join-Path $sourceRoot '*.md') -Destination $cloneRoot -Force

    $homePath = Join-Path $cloneRoot 'Home.md'
    if (-not (Test-Path -LiteralPath $homePath)) {
        $homeLines = @(
            '# Melanopsin',
            '',
            ('See the [API reference](https://github.com/{0}/wiki/API-Reference).' -f $Repository)
        )
        [System.IO.File]::WriteAllText($homePath, ($homeLines -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
    }

    git add --all
    git -c user.name='github-actions[bot]' -c user.email='41898282+github-actions[bot]@users.noreply.github.com' diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host 'The wiki is already up to date.'
        return
    }

    git -c user.name='github-actions[bot]' -c user.email='41898282+github-actions[bot]@users.noreply.github.com' commit --quiet -m $CommitMessage
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not commit the wiki changes.'
    }

    git push --quiet origin HEAD
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not push the wiki changes.'
    }

    Write-Host "Published the API reference to https://github.com/$Repository/wiki/API-Reference."
} finally {
    # Never leave the token behind in the clone under artifacts/.
    git remote set-url origin $remoteUrl
    Pop-Location
}
