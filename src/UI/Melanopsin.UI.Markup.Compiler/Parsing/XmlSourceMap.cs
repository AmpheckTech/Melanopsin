using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Parsing;

internal sealed class XmlSourceMap {
    private readonly IReadOnlyList<int> _lineStarts;

    public XmlSourceMap(string source) {
        Source = source;
        var lineStarts = new List<int> { 0 };
        for (var index = 0; index < source.Length; index++) {
            if (source[index] == '\n') {
                lineStarts.Add(index + 1);
            }
        }

        _lineStarts = lineStarts;
    }

    /// <summary>The document the offsets are expressed in.</summary>
    public string Source { get; }

    public int Offset(XObject source) {
        if (source is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo()) {
            return 0;
        }

        return Offset(lineInfo.LineNumber, lineInfo.LinePosition);
    }

    /// <summary>The offset of a one-based line and column, as the XML reader reports them.</summary>
    public int Offset(int lineNumber, int linePosition) {
        if (lineNumber <= 0 || _lineStarts.Count == 0) {
            return 0;
        }

        var lineIndex = Math.Min(lineNumber - 1, _lineStarts.Count - 1);
        return _lineStarts[lineIndex] + Math.Max(0, linePosition - 1);
    }

    public MarkupDiagnostic CreateDiagnostic(XmlException exception) {
        return new MarkupDiagnostic(
            MarkupDiagnosticCodes.InvalidSyntax,
            $"Invalid XML: {exception.Message}",
            Offset(exception.LineNumber, exception.LinePosition),
            1);
    }
}
