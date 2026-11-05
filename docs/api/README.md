# API reference

The public API reference of the packages is published to the
[GitHub wiki](https://github.com/AmpheckTech/Melanopsin/wiki/API-Reference).
No generated Markdown is committed to this repository: the pages are built in CI and
pushed to the wiki, so the repository only keeps the generator configuration and the
scripts that drive it.

## How it works

```text
src/**                                      (dotnet build -c Release)
  -> <Assembly>.xml                         XML documentation file per assembly
  -> artifacts/docs/api/<Assembly>/**/index.md    (defaultdocumentation.console)
  -> artifacts/wiki/<Page>.md               (prepare-wiki.ps1, flat wiki pages)
  -> <repository>.wiki.git                  (publish-wiki.ps1, git push)
```

`.github/workflows/wiki.yml` runs that chain on every push to `main` that touches
`src/**`, `external/**`, `docs/api/**`, `Directory.Build.props`, `.editorconfig`,
`global.json` or the tool manifest, and on manual dispatch.

Both `artifacts/docs/api` and `artifacts/wiki` are covered by the `artifacts/` entry in
`.gitignore`, so a local run never dirties the working tree.

## One-time setup

1. Open the repository wiki and create the first page (the default `Home` page is fine).
   A wiki git repository cannot be cloned before it has a page.
2. Create a token that is allowed to push to the wiki and store it as the repository
   secret `WIKI_TOKEN` (Settings -> Secrets and variables -> Actions -> New secret):
   - classic personal access token with the `repo` scope, or
   - a token of an account with write access to the repository and the wiki.
3. Optional: disable public editing of the wiki, the pages are machine generated.

This is required because the `GITHUB_TOKEN` a workflow receives is scoped to the
workflow repository and is not accepted by the `<owner>/<repo>.wiki.git` remote, and
because wiki repositories have no deploy key settings of their own.

Without `WIKI_TOKEN` the workflow still builds the pages and uploads them as the
`api-reference-wiki` artifact, then stops with a warning instead of failing.

## Local run

```pwsh
# from the repository root
pwsh -File docs/api/generate.ps1                 # -> artifacts/docs/api
pwsh -File docs/api/prepare-wiki.ps1             # -> artifacts/wiki (open the pages here)
```

Publishing is only needed when the wiki has to be updated without a push to `main`:

```pwsh
$env:WIKI_TOKEN = '<token>'
$env:GITHUB_REPOSITORY = 'AmpheckTech/Melanopsin'
pwsh -File docs/api/publish-wiki.ps1
```

## Scripts

| Script | Purpose |
| :--- | :--- |
| `generate.ps1` | Builds the solution in Release and runs `defaultdocumentation.console` for every assembly in `assemblies.json` into `artifacts/docs/api`. |
| `prepare-wiki.ps1` | Converts the nested `index.md` tree into flat wiki pages, rewrites relative links into absolute wiki URLs, and writes `API-Reference.md`, `_Sidebar.md` and `_Footer.md`. |
| `publish-wiki.ps1` | Clones `<repository>.wiki.git`, replaces the pages it owns, commits and pushes. `Home.md` and any hand written page are preserved. |
| `assemblies.csv` | The documented assemblies, in the order used by the wiki index and sidebar. |
| `defaultdocumentation.json` | Shared `defaultdocumentation.console` settings. |

## Wiki layout

Wiki page titles cannot contain `\ / : * ? " < > |` (see the GitHub documentation),
so the nested documentation tree is flattened:

| Documented item | Page title |
| :--- | :--- |
| assembly `Melanopsin.UI` | `Melanopsin.UI-Overview` |
| namespace `Melanopsin.UI.Threading` | `Melanopsin.UI.Threading` |
| type `Melanopsin.UI.Threading.UiDispatcher` | `Melanopsin.UI.Threading.UiDispatcher` |
| index of all assemblies | `API-Reference` |

Generator settings live in `defaultdocumentation.json`:

| Setting | Value |
| :--- | :--- |
| `GeneratedPages` | `Assembly,Namespaces,Types` |
| `GeneratedAccessModifiers` | `Public,Protected` |
| `IncludeUndocumentedItems` | `false` - only members with `///` comments are documented. |
| `FileNameFactory` | `DirectoryName` - one folder per item with a trailing `index.md`. |
| `Markdown.TableOfContentsModes` | `Grouped,IncludeSummaryWithNewLine` |
| `Markdown.HandleLineBreak` | `true` |

## Documented assemblies

Only consumer facing packages are documented; the analyzer
(`Melanopsin.UI.Markup.Compiler`), the language server, the resource compiler and the
demo sample are not part of the published surface. Edit `assemblies.csv` to add or
remove an assembly: the generator and the wiki index both read that file.

`GeneratedAccessModifiers` is `Public,Protected`, so `internal` types are skipped, and
`IncludeUndocumentedItems` is `false`, so assemblies without `///` comments (currently
`Melanopsin`) publish only a handful of pages.

## XML documentation in the build

`Directory.Build.props` enables `GenerateDocumentationFile` for every project while
`TreatWarningsAsErrors` stays on. `.editorconfig` keeps the missing-comment diagnostics
from breaking the build by downgrading them to `suggestion`: `CS1572`, `CS1573`,
`CS1574`, `CS1580`, `CS1584`, `CS1591`, `CS1711`, `CS1712` and `CS1734`.

Analyzer severities do not apply to source generated trees, so generators that emit
public members write `#pragma warning disable CS1591` in their file header themselves.

## Limitations

- Types from other packages link to their `learn.microsoft.com` page instead of a page in
  this wiki. Wiring cross-assembly links requires the two pass `-l`/`-e` invocation of
  `defaultdocumentation.console`, which this workflow does not use.
- The wiki has a soft limit of 5,000 files; the current reference uses roughly 220 pages.
- Wiki page titles are global. A generated title always starts with the assembly name, so
  collisions with hand written pages are unlikely, but a hand written page named exactly
  like a generated one is overwritten by the next run.


