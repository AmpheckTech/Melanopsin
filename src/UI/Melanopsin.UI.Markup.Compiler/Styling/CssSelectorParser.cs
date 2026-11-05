// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssSelectorParser {
    public static bool TryParse(
        string source,
        int offset,
        out CssComplexSelector? selector,
        out CssDiagnostic? diagnostic) {
        var parser = new Parser(source, offset);
        selector = parser.ParseFull();
        diagnostic = parser.Diagnostic;
        return selector is not null;
    }

    public static bool TryParseNested(
        string source,
        int offset,
        CssComplexSelector parent,
        out CssComplexSelector? selector,
        out CssDiagnostic? diagnostic) {
        var parser = new Parser(source, offset);
        selector = parser.ParseNested(parent);
        diagnostic = parser.Diagnostic;
        return selector is not null;
    }

    private sealed partial class Parser {
        private readonly string _source;
        private readonly int _offset;
        private int _index;

        public Parser(string source, int offset) {
            _source = source;
            _offset = offset;
        }

        public CssDiagnostic? Diagnostic { get; private set; }

        public CssComplexSelector? ParseFull() {
            if (_source.Length == 0) {
                return Error("The selector is empty.");
            }

            if (_source[_index] == '&') {
                return Error("'&' is only valid at the beginning of a nested selector.");
            }

            var current = ParseCompound(allowType: true);
            if (current is null) {
                return null;
            }

            var result = new CssComplexSelector(current);
            return ParseRemainder(result);
        }

        public CssComplexSelector? ParseNested(CssComplexSelector parent) {
            if (_source.Length == 0 || _source[0] != '&') {
                return Error("A nested selector must begin with '&'.");
            }

            _index = 1;
            var suffix = ParseCompoundSuffix();
            if (suffix is null) {
                return null;
            }

            if (parent.Rightmost.PseudoElement is not null) {
                if (suffix.PseudoElement is not null) {
                    return Error(
                        "A nested selector cannot replace an inherited pseudo-element.");
                }

                if (suffix.Id is not null || suffix.Classes.Count != 0) {
                    return Error(
                        "A pseudo-element cannot be refined by ids or classes in a nested selector.");
                }

                suffix = new CssCompoundSelector(
                    pseudoElementStates: [.. suffix.States, .. suffix.PseudoElementStates]);
            }

            CssCompoundSelector rightmost;
            try {
                rightmost = parent.Rightmost.Append(suffix);
            } catch (InvalidOperationException exception) {
                return Error(exception.Message);
            }

            var result = new CssComplexSelector(rightmost, parent.Parent, parent.Combinator);
            return ParseRemainder(result);
        }

        private CssComplexSelector? ParseRemainder(CssComplexSelector result) {
            if (result.Rightmost.PseudoElement is not null &&
                _index < _source.Length) {
                return Error("A pseudo-element must be the final target of a selector.");
            }

            if (_index < _source.Length && _source[_index] == '&') {
                return Error("A nested selector may contain '&' only once, at its beginning.");
            }

            var current = result;
            while (_index < _source.Length) {
                var hadWhitespace = SkipWhitespace();
                if (_index >= _source.Length) {
                    return null;
                }

                CssCombinator combinator;
                if (_source[_index] == '>') {
                    combinator = CssCombinator.Child;
                    _index++;
                    SkipWhitespace();
                } else if (hadWhitespace) {
                    combinator = CssCombinator.Descendant;
                } else {
                    return Error($"Unexpected character '{_source[_index]}' in selector '{_source}'.");
                }

                var compound = ParseCompound(allowType: true);
                if (compound is null) {
                    return null;
                }

                current = new CssComplexSelector(compound, current, combinator);
            }

            return current;
        }

    }
}
