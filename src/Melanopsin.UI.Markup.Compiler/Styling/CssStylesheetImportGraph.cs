using System;
using System.Collections.Generic;
using System.IO;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static class CssStylesheetImportGraph {
    public static void ResolveImports(
        IReadOnlyDictionary<string, CssStylesheet> sheetsByPath,
        IDictionary<string, List<CssResolvedImport>> importsByPath,
        IDictionary<string, List<CssDiagnostic>> diagnosticsByPath) {
        foreach (var stylesheet in sheetsByPath.Values) {
            var imports = new List<CssResolvedImport>();
            var diagnostics = new List<CssDiagnostic>();
            var path = CssStylesheetPath.Normalize(stylesheet.Path);
            importsByPath.Add(path, imports);
            diagnosticsByPath.Add(path, diagnostics);
            foreach (var import in stylesheet.Imports) {
                if (Path.IsPathRooted(import.Path)) {
                    diagnostics.Add(new CssDiagnostic(
                        MarkupDiagnosticCodes.InvalidStylesheet,
                        "Stylesheet imports must use a path relative to the importing file.",
                        import.Offset,
                        import.Length));
                    continue;
                }

                string importedPath;
                try {
                    importedPath = CssStylesheetPath.Normalize(Path.Combine(
                        Path.GetDirectoryName(stylesheet.Path) ?? string.Empty,
                        import.Path.Replace('/', Path.DirectorySeparatorChar)));
                } catch (Exception exception) when (
                    exception is ArgumentException or NotSupportedException or PathTooLongException) {
                    diagnostics.Add(new CssDiagnostic(
                        MarkupDiagnosticCodes.InvalidStylesheet,
                        $"Invalid stylesheet import path '{import.Path}'.",
                        import.Offset,
                        import.Length));
                    continue;
                }

                if (!sheetsByPath.TryGetValue(importedPath, out var imported)) {
                    diagnostics.Add(new CssDiagnostic(
                        MarkupDiagnosticCodes.InvalidStylesheet,
                        $"Imported stylesheet '{import.Path}' is not included as an AdditionalFile.",
                        import.Offset,
                        import.Length));
                    continue;
                }

                imports.Add(new CssResolvedImport(import, imported));
            }
        }
    }

    public static void FindInvalidSheets(
        IReadOnlyDictionary<string, CssStylesheet> sheetsByPath,
        IReadOnlyDictionary<string, List<CssDiagnostic>> diagnosticsByPath,
        ISet<string> invalidPaths) {
        foreach (var stylesheet in sheetsByPath.Values) {
            var path = CssStylesheetPath.Normalize(stylesheet.Path);
            if (stylesheet.Diagnostics.Count != 0 ||
                diagnosticsByPath[path].Count != 0) {
                invalidPaths.Add(path);
            }
        }
    }

    public static void AnalyzeImports(
        IReadOnlyDictionary<string, CssStylesheet> sheetsByPath,
        IReadOnlyDictionary<string, List<CssResolvedImport>> importsByPath,
        IDictionary<string, List<CssDiagnostic>> diagnosticsByPath,
        ISet<string> invalidPaths) {
        var comparer = CssStylesheetPath.Comparer;
        var visiting = new HashSet<string>(comparer);
        var visited = new HashSet<string>(comparer);
        foreach (var stylesheet in sheetsByPath.Values) {
            Visit(stylesheet);
        }

        void Visit(CssStylesheet stylesheet) {
            var path = CssStylesheetPath.Normalize(stylesheet.Path);
            if (!visited.Add(path)) {
                return;
            }

            visiting.Add(path);
            foreach (var resolvedImport in importsByPath[path]) {
                var importedPath = CssStylesheetPath.Normalize(resolvedImport.Target.Path);
                if (visiting.Contains(importedPath)) {
                    diagnosticsByPath[path].Add(new CssDiagnostic(
                        MarkupDiagnosticCodes.InvalidStylesheet,
                        $"Cyclic stylesheet import '{resolvedImport.Import.Path}'.",
                        resolvedImport.Import.Offset,
                        resolvedImport.Import.Length));
                    foreach (var cyclePath in visiting) {
                        invalidPaths.Add(cyclePath);
                    }

                    continue;
                }

                if (!visited.Contains(importedPath)) {
                    Visit(resolvedImport.Target);
                }
            }

            visiting.Remove(path);
        }

        bool propagated;
        do {
            propagated = false;
            foreach (var stylesheet in sheetsByPath.Values) {
                var path = CssStylesheetPath.Normalize(stylesheet.Path);
                if (invalidPaths.Contains(path)) {
                    continue;
                }

                foreach (var resolvedImport in importsByPath[path]) {
                    if (!invalidPaths.Contains(
                            CssStylesheetPath.Normalize(resolvedImport.Target.Path))) {
                        continue;
                    }

                    diagnosticsByPath[path].Add(new CssDiagnostic(
                        MarkupDiagnosticCodes.InvalidStylesheet,
                        $"Imported stylesheet '{resolvedImport.Import.Path}' is invalid.",
                        resolvedImport.Import.Offset,
                        resolvedImport.Import.Length));
                    invalidPaths.Add(path);
                    propagated = true;
                    break;
                }
            }
        } while (propagated);
    }
}
