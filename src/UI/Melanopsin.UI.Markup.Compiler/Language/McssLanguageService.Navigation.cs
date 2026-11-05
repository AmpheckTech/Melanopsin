// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class McssLanguageService {
    public static McssSourceSpan? FindDefinition(
        McssAnalysis analysis,
        string source,
        int offset) {
        var position = Math.Min(Math.Max(offset, 0), source.Length);
        foreach (var import in analysis.Imports) {
            if (position >= import.Offset && position <= import.Offset + import.Length &&
                import.TargetPath is { } targetPath) {
                return new McssSourceSpan(targetPath, 0, 0);
            }
        }

        if (IsInsideCommentOrString(source, position) ||
            WordAt(source, position) is not { } word) {
            return null;
        }

        var name = source.Substring(word.Start, word.Length);
        var token = FindToken(analysis, name);
        return token is null
            ? null
            : new McssSourceSpan(analysis.Path, token.NameOffset, token.NameLength);
    }

    public static IReadOnlyList<McssSourceSpan> FindTokenSpans(
        McssAnalysis analysis,
        string source,
        string name) {
        var definition = FindToken(analysis, name);
        if (definition is null) {
            return Array.Empty<McssSourceSpan>();
        }

        var spans = new List<McssSourceSpan>();
        var index = 0;
        while (index < source.Length) {
            if (StartsComment(source, index)) {
                index = SkipComment(source, index);
                continue;
            }

            if (source[index] is '\'' or '"') {
                index = SkipString(source, index);
                continue;
            }

            if (!IsIdentifierCharacter(source[index])) {
                index++;
                continue;
            }

            var start = index++;
            while (index < source.Length && IsIdentifierCharacter(source[index])) {
                index++;
            }

            if (!source.AsSpan(start, index - start).Equals(name, StringComparison.Ordinal)) {
                continue;
            }

            if (start == definition.NameOffset && index - start == definition.NameLength) {
                spans.Add(new McssSourceSpan(analysis.Path, start, index - start));
            } else if (start > 0 && source[start - 1] == '$') {
                spans.Add(new McssSourceSpan(analysis.Path, start - 1, index - start + 1));
            }
        }

        return spans
            .GroupBy(static span => span.Offset)
            .Select(static group => group.First())
            .OrderBy(static span => span.Offset)
            .ToArray();
    }
}
