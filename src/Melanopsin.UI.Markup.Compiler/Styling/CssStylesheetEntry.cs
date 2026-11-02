using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal sealed class CssStylesheetEntry {
    public CssStylesheetEntry(
        CssStylesheet stylesheet,
        IReadOnlyList<CssStylesheet> imports,
        IReadOnlyList<CssDiagnostic> diagnostics,
        bool isValid) {
        Stylesheet = stylesheet;
        Imports = imports;
        Diagnostics = diagnostics;
        IsValid = isValid;
    }

    public CssStylesheet Stylesheet { get; }

    public IReadOnlyList<CssStylesheet> Imports { get; }

    public IReadOnlyList<CssDiagnostic> Diagnostics { get; }

    public bool IsValid { get; }
}

internal sealed class CssResolvedImport {
    public CssResolvedImport(CssImport import, CssStylesheet target) {
        Import = import;
        Target = target;
    }

    public CssImport Import { get; }

    public CssStylesheet Target { get; }
}
