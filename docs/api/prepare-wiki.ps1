# Converts the DefaultDocumentation output (one folder per item, every page named
# index.md) into the flat page layout GitHub wikis require.
#
# Wiki page titles may not contain \ / : * ? " < > |, so nested pages are not an option:
# every page becomes a single file whose name is the page title. Relative *.md links are
# rewritten into absolute wiki URLs, the link format GitHub documents for wikis.
#
# Usage (from the repository root):
#     pwsh -File docs/api/prepare-wiki.ps1
#     pwsh -File docs/api/prepare-wiki.ps1 -SourceRoot artifacts/docs/api -DestinationRoot artifacts/wiki

[CmdletBinding()]
param(
    [string]$SourceRoot = 'artifacts/docs/api',
    [string]$DestinationRoot = 'artifacts/wiki',
    [string]$Repository = ''
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
$destinationRoot = Resolve-RepositoryPath $DestinationRoot

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Nothing to convert: $sourceRoot does not exist. Run docs/api/generate.ps1 first."
}

if ([string]::IsNullOrWhiteSpace($Repository)) {
    $Repository = $env:GITHUB_REPOSITORY
}

if ([string]::IsNullOrWhiteSpace($Repository)) {
    $propsPath = Join-Path $repositoryRoot 'Directory.Build.props'
    $props = [xml](Get-Content -LiteralPath $propsPath -Raw)
    $repositoryUrl = ($props.Project.PropertyGroup.RepositoryUrl | Where-Object { $_ } | Select-Object -First 1)
    if ($repositoryUrl -match 'github\.com[/:](?<owner>[^/]+)/(?<repo>[^/\.]+)') {
        $Repository = "$($Matches.owner)/$($Matches.repo)"
    }
}

if ([string]::IsNullOrWhiteSpace($Repository)) {
    throw 'Could not determine the GitHub repository; pass -Repository owner/name.'
}

$wikiBaseUrl = "https://github.com/$Repository/wiki"

# Characters GitHub documents as unusable in wiki page titles, plus '#' (anchor separator),
# '%' (URL escape) and brackets so a title always survives being used inside a URL.
$invalidTitleCharacters = @('\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '%', '[', ']')

function ConvertTo-WikiPageTitle([string]$Title) {
    foreach ($character in $invalidTitleCharacters) {
        $Title = $Title.Replace($character, '-')
    }

    $Title = ($Title -replace '\s+', '-')
    return $Title.Trim('-', '.')
}

function Get-WikiPageUrl([string]$Title) {
    return '{0}/{1}' -f $wikiBaseUrl, [System.Uri]::EscapeDataString($Title)
}

# Map every index.md of the DefaultDocumentation tree to the wiki page title it becomes.
$pageTitles = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$pageSources = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($file in (Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter 'index.md' -File)) {
    $relative = $file.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
    # Drop the trailing index.md: the folder path itself names the documented item.
    $segments = @($relative -split '[\\/]' | Select-Object -SkipLast 1)
    if ($segments.Count -eq 0) {
        continue
    }

    # First segment is the assembly folder, the rest is the namespace/type path.
    $title = if ($segments.Count -eq 1) {
        '{0}-Overview' -f $segments[0]
    } else {
        ($segments | Select-Object -Skip 1) -join '.'
    }

    $title = ConvertTo-WikiPageTitle $title
    if ($pageSources.ContainsKey($title)) {
        throw "Wiki page title collision on '$title': $relative and $($pageSources[$title])."
    }

    $pageSources[$title] = $relative
    $pageTitles[$file.FullName] = $title
}

if ($pageTitles.Count -eq 0) {
    throw "No generated pages found under $sourceRoot."
}

if (Test-Path -LiteralPath $destinationRoot) {
    Remove-Item -LiteralPath $destinationRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null

# Matches the relative target of a Markdown link, for example "../../../index.md#Type.Member".
# The anchor may contain parentheses, so it ends at a quote or whitespace instead of ')'.
$linkPattern = [regex]"(?<=\]\()(?<target>(?:[^/\s()]*/)*index\.md(?:#[^'\s]*)?)"

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

foreach ($entry in $pageTitles.GetEnumerator()) {
    $sourceFile = $entry.Key
    $title = $entry.Value
    $sourceDirectory = Split-Path -Parent $sourceFile
    $content = [System.IO.File]::ReadAllText($sourceFile, [System.Text.Encoding]::UTF8)

    $rewritten = $linkPattern.Replace($content, [System.Text.RegularExpressions.MatchEvaluator] {
            param($match)

            $target = $match.Groups['target'].Value
            $anchorIndex = $target.IndexOf('#')
            $relativePath = if ($anchorIndex -ge 0) { $target.Substring(0, $anchorIndex) } else { $target }
            $anchor = if ($anchorIndex -ge 0) { $target.Substring($anchorIndex + 1) } else { '' }

            $decodedPath = [System.Uri]::UnescapeDataString($relativePath)
            $absolutePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($sourceDirectory, $decodedPath))

            if (-not $pageTitles.ContainsKey($absolutePath)) {
                return $match.Value
            }

            $url = Get-WikiPageUrl $pageTitles[$absolutePath]
            if (-not [string]::IsNullOrEmpty($anchor)) {
                # Parentheses in member anchors would end the Markdown link early.
                $url = '{0}#{1}' -f $url, [System.Uri]::EscapeDataString($anchor)
            }

            return $url
        })

    [System.IO.File]::WriteAllText((Join-Path $destinationRoot ($title + '.md')), $rewritten, $utf8NoBom)
}

# Navigation pages owned by this script. Home.md is left alone: it belongs to the wiki.
$assemblies = @(Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'assemblies.csv') -Encoding UTF8)

$indexLines = [System.Collections.Generic.List[string]]::new()
$indexLines.Add('# Melanopsin API reference')
$indexLines.Add('')
$indexLines.Add('Generated from the XML documentation comments in the source code.')
$indexLines.Add('Change the `///` comments and push to `main`; do not edit these pages by hand.')
$indexLines.Add('')
$indexLines.Add('| Assembly | Summary |')
$indexLines.Add('| :--- | :--- |')

$sidebarLines = [System.Collections.Generic.List[string]]::new()
$sidebarLines.Add('### Melanopsin API')
$sidebarLines.Add('')
$sidebarLines.Add(('- [API reference]({0})' -f (Get-WikiPageUrl 'API-Reference')))
$sidebarLines.Add('')

foreach ($assembly in $assemblies) {
    $url = Get-WikiPageUrl (ConvertTo-WikiPageTitle ('{0}-Overview' -f $assembly.Assembly))
    $summary = $assembly.Summary -replace '\|', '\|'
    $indexLines.Add(('| [{0}]({1}) | {2} |' -f $assembly.Assembly, $url, $summary))
    $sidebarLines.Add(('- [{0}]({1})' -f $assembly.Assembly, $url))
}

$indexLines.Add('')
$indexLines.Add('Members without XML documentation comments are omitted, and types from other')
$indexLines.Add('assemblies link to their `learn.microsoft.com` page.')

[System.IO.File]::WriteAllText((Join-Path $destinationRoot 'API-Reference.md'), ($indexLines -join "`n") + "`n", $utf8NoBom)
[System.IO.File]::WriteAllText((Join-Path $destinationRoot '_Sidebar.md'), ($sidebarLines -join "`n") + "`n", $utf8NoBom)

$footerLines = [System.Collections.Generic.List[string]]::new()
$footerLines.Add('---')
$footerLines.Add('')
$footerLines.Add('_API pages are generated from the `///` comments in the source code._')
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
    $shortSha = $env:GITHUB_SHA.Substring(0, 7)
    $footerLines.Add('')
    $footerLines.Add(('Built from [{0}](https://github.com/{1}/commit/{2}) on {3}.' -f $shortSha, $Repository, $env:GITHUB_SHA, [System.DateTime]::UtcNow.ToString('yyyy-MM-dd')))
}

[System.IO.File]::WriteAllText((Join-Path $destinationRoot '_Footer.md'), ($footerLines -join "`n") + "`n", $utf8NoBom)

Write-Host "Converted $($pageTitles.Count) pages into wiki pages under $destinationRoot."

