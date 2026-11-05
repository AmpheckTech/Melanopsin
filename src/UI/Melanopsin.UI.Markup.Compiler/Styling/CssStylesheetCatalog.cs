using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal sealed class CssStylesheetCatalog {
    private CssStylesheetCatalog(IReadOnlyList<CssStylesheetEntry> entries) {
        Entries = entries;
    }

    public IReadOnlyList<CssStylesheetEntry> Entries { get; }

    public static CssStylesheetCatalog Create(IReadOnlyList<CssStylesheet> stylesheets) {
        var comparer = CssStylesheetPath.Comparer;
        var sheetsByPath = CssStylesheetPath.CreateIndex(stylesheets);
        var importsByPath = new Dictionary<string, List<CssResolvedImport>>(comparer);
        var diagnosticsByPath = new Dictionary<string, List<CssDiagnostic>>(comparer);
        CssStylesheetImportGraph.ResolveImports(
            sheetsByPath,
            importsByPath,
            diagnosticsByPath);

        var invalidPaths = new HashSet<string>(comparer);
        CssStylesheetImportGraph.FindInvalidSheets(
            sheetsByPath,
            diagnosticsByPath,
            invalidPaths);
        CssStylesheetImportGraph.AnalyzeImports(
            sheetsByPath,
            importsByPath,
            diagnosticsByPath,
            invalidPaths);

        var entries = sheetsByPath
            .OrderBy(static pair => pair.Key, System.StringComparer.Ordinal)
            .Select(pair => new CssStylesheetEntry(
                pair.Value,
                importsByPath[pair.Key].Select(static import => import.Target).ToArray(),
                diagnosticsByPath[pair.Key],
                !invalidPaths.Contains(pair.Key)))
            .ToArray();
        return new CssStylesheetCatalog(entries);
    }
}
