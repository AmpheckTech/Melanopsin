using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssSelectorParser {
    private sealed partial class Parser {
        private CssCompoundSelector? ParseCompound(bool allowType) {
            string? typeName = null;
            var hasCompound = false;
            if (_index < _source.Length && _source[_index] == '*') {
                if (!allowType) {
                    Error("'*' is not valid after '&'.");
                    return null;
                }

                _index++;
                hasCompound = true;
            } else if (_index < _source.Length && IsIdentifierStart(_source[_index])) {
                if (!allowType) {
                    Error("A type cannot follow '&' inside the same compound selector.");
                    return null;
                }

                typeName = ReadIdentifier();
                if (typeName is null) {
                    return null;
                }

                hasCompound = true;
            }

            var suffix = ParseCompoundSuffix();
            if (suffix is null) {
                return null;
            }

            if (!hasCompound &&
                suffix.Id is null &&
                suffix.Classes.Count == 0 &&
                suffix.States.Count == 0 &&
                suffix.PseudoElement is null) {
                Error($"Expected a selector compound near '{_source.Substring(_index)}'.");
                return null;
            }

            return new CssCompoundSelector(typeName).Append(suffix);
        }

        private CssCompoundSelector? ParseCompoundSuffix() {
            string? id = null;
            List<string>? classes = null;
            List<string>? states = null;
            string? pseudoElement = null;
            List<string>? pseudoElementStates = null;
            while (_index < _source.Length) {
                var marker = _source[_index];
                if (marker is not ('#' or '.' or ':')) {
                    break;
                }

                _index++;
                var isPseudoElement = marker == ':' &&
                    _index < _source.Length &&
                    _source[_index] == ':';
                if (isPseudoElement) {
                    _index++;
                }

                var identifier = ReadIdentifier();
                if (identifier is null) {
                    return null;
                }

                if (isPseudoElement) {
                    if (pseudoElement is not null) {
                        Error("A compound selector cannot contain multiple pseudo-elements.");
                        return null;
                    }

                    pseudoElement = identifier;
                    continue;
                }

                if (pseudoElement is not null) {
                    if (marker != ':') {
                        Error("Only pseudo-class states may follow a pseudo-element.");
                        return null;
                    }

                    pseudoElementStates ??= [];
                    pseudoElementStates.Add(identifier);
                    continue;
                }

                if (marker == '#') {
                    if (id is not null) {
                        Error("A compound selector cannot contain multiple ids.");
                        return null;
                    }

                    id = identifier;
                } else if (marker == '.') {
                    classes ??= [];
                    classes.Add(identifier);
                } else {
                    states ??= [];
                    states.Add(identifier);
                }
            }

            if (_index < _source.Length &&
                !char.IsWhiteSpace(_source[_index]) &&
                _source[_index] != '>') {
                Error($"Unsupported selector syntax near '{_source.Substring(_index)}'.");
                return null;
            }

            return new CssCompoundSelector(
                null,
                id,
                classes,
                states,
                pseudoElement,
                pseudoElementStates);
        }
    }
}
