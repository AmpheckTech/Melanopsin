using System;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssSelectorParser {
    private sealed partial class Parser {
        private string? ReadIdentifier() {
            if (_index >= _source.Length || !IsIdentifierStart(_source[_index])) {
                Error("Expected a selector identifier.");
                return null;
            }

            var start = _index++;
            while (_index < _source.Length && IsIdentifierPart(_source[_index])) {
                _index++;
            }

            return _source.Substring(start, _index - start);
        }

        private bool SkipWhitespace() {
            var start = _index;
            while (_index < _source.Length && char.IsWhiteSpace(_source[_index])) {
                _index++;
            }

            return _index != start;
        }

        private CssComplexSelector? Error(string message) {
            Diagnostic = new CssDiagnostic(
                MarkupDiagnosticCodes.InvalidStylesheet,
                message,
                _offset + Math.Min(_index, Math.Max(0, _source.Length - 1)),
                Math.Max(1, _source.Length));
            return null;
        }

        private static bool IsIdentifierStart(char character) {
            return char.IsLetter(character) || character == '_';
        }

        private static bool IsIdentifierPart(char character) {
            return char.IsLetterOrDigit(character) || character is '_' or '-';
        }
    }
}
