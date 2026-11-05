// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.LanguageServer.LanguageFeatures;

internal static class PositionMapper {
    public static (int Line, int Character) ToPosition(string text, int offset) {
        var position = Math.Min(Math.Max(offset, 0), text.Length);
        var line = 0;
        var lineStart = 0;
        for (var index = 0; index < position; index++) {
            if (text[index] == '\n') {
                line++;
                lineStart = index + 1;
            }
        }

        // LSP defaults to UTF-16 code units, which is also the indexing unit of
        // System.String. Keeping this explicit prevents a future UTF-8 switch
        // from silently using the wrong character count.
        return (line, position - lineStart);
    }

    public static int ToOffset(string text, int line, int character) {
        var currentLine = 0;
        var index = 0;
        while (index < text.Length && currentLine < line) {
            if (text[index++] == '\n') {
                currentLine++;
            }
        }

        var offset = Math.Min(text.Length, index + Math.Max(0, character));
        return offset > 0 && offset < text.Length &&
               char.IsLowSurrogate(text[offset]) &&
               char.IsHighSurrogate(text[offset - 1])
            ? offset - 1
            : offset;
    }

    public static object Range(string text, int start, int length) {
        var end = Math.Min(text.Length, start + Math.Max(0, length));
        var (startLine, startCharacter) = ToPosition(text, start);
        var (endLine, endCharacter) = ToPosition(text, end);
        return new {
            start = new { line = startLine, character = startCharacter },
            end = new { line = endLine, character = endCharacter },
        };
    }

    public static object RangePositions(int startLine, int startCharacter, int endLine, int endCharacter) {
        return new {
            start = new { line = startLine, character = startCharacter },
            end = new { line = endLine, character = endCharacter },
        };
    }
}
