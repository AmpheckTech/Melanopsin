# Regenerates the Markdown API reference into artifacts/docs/api.
#
# The output stays out of the repository (.gitignore ignores artifacts/):
# .github/workflows/wiki.yml converts it into flat wiki pages and pushes them to this
# repository's GitHub wiki. Run prepare-wiki.ps1 locally to preview that conversion.
#
# Every package is documented in its own output folder: DefaultDocumentation
# clears the *.md files of its output folder before writing, so a shared folder
# would delete the pages produced by the previous assembly.
#
# Usage (from the repository root):
#     pwsh -File docs/api/generate.ps1
#     pwsh -File docs/api/generate.ps1 -OutputRoot artifacts/docs/api

[CmdletBinding()]
param(
    [string]$OutputRoot = 'artifacts/docs/api'
)

$ErrorActionPreference = 'Stop'

# Resolve every path against the repository root so the script can be started from
# anywhere. DefaultDocumentation resolves a relative output folder against its own
# settings directory, which would nest the output under docs/api/docs/api.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$configPath = Join-Path $PSScriptRoot 'defaultdocumentation.json'
$assembliesPath = Join-Path $PSScriptRoot 'assemblies.csv'

$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputRoot))
}

# Single source of truth for the documented assemblies, shared with prepare-wiki.ps1.
# Markup.Compiler (analyzer), LanguageServer and ResourceCompiler (tools) plus the Demo
# sample are intentionally absent: they are not part of the public API surface.
$assemblies = @(Import-Csv -LiteralPath $assembliesPath -Encoding UTF8)

Push-Location $repositoryRoot
try {
    dotnet tool restore
    dotnet build Melanopsin.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Build failed, documentation was not generated.'
    }

    # Start clean so assemblies dropped from assemblies.json do not linger.
    if (Test-Path -LiteralPath $outputRoot) {
        Remove-Item -LiteralPath $outputRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

    foreach ($entry in $assemblies) {
        $project = $entry.Project
        $assemblyPath = (dotnet msbuild $project -getProperty:TargetPath -p:Configuration=Release).Trim()
        if (-not (Test-Path -LiteralPath $assemblyPath)) {
            throw "Assembly not found: $assemblyPath"
        }

        $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($assemblyPath)
        $documentationPath = [System.IO.Path]::ChangeExtension($assemblyPath, '.xml')

        if (-not (Test-Path -LiteralPath $documentationPath)) {
            # Projects outside the repository build props (the Mathematics submodule has its
            # own Directory.Build.props) do not inherit GenerateDocumentationFile. Build that
            # single project once with the property forced, keeping its warnings non fatal so
            # the missing XML comments do not fail this documentation-only build.
            Write-Host "Enabling XML documentation for $assemblyName..."
            dotnet build $project -c Release --nologo -p:GenerateDocumentationFile=true -p:TreatWarningsAsErrors=false
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $documentationPath)) {
                throw "Could not produce the XML documentation file for $project"
            }
        }

        Write-Host "Generating API reference for $assemblyName..."

        $outputPath = Join-Path $outputRoot $assemblyName
        dotnet tool run defaultdocumentation -- -j $configPath -a $assemblyPath -o $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw "defaultdocumentation failed for $assemblyPath"
        }
    }

    Write-Host "Done. API reference written to $outputRoot."
} finally {
    Pop-Location
}
