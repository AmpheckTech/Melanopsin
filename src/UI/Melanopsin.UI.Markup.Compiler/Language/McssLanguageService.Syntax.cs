// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Language;

public static partial class McssLanguageService {
    private enum SourceBlockKind {
        TopLevel,
        Style,
        Tokens,
    }

    private readonly record struct BlockContext(
        SourceBlockKind Kind,
        int HeaderStart,
        int ContentStart,
        int End);

    private readonly record struct SourceWord(int Start, int Length) {
        public int End => Start + Length;
    }

    private readonly record struct SourceStatement(
        int Start,
        int End,
        int Colon,
        int Equal,
        bool IsBlockHeader);

    private static SourceWord? WordAt(string source, int offset) {
        var position = Math.Min(Math.Max(offset, 0), source.Length);
        if (position == source.Length || !IsIdentifierCharacter(source[position])) {
            position--;
        }

        if (position < 0 || position >= source.Length ||
            !IsIdentifierCharacter(source[position])) {
            return null;
        }

        var start = position;
        while (start > 0 && IsIdentifierCharacter(source[start - 1])) {
            start--;
        }

        var end = position + 1;
        while (end < source.Length && IsIdentifierCharacter(source[end])) {
            end++;
        }

        return new SourceWord(start, end - start);
    }

    private static bool IsWordCharacter(char value) {
        return IsIdentifierCharacter(value) || value is '$' or '@' or '.' or '#';
    }

    private static bool IsIdentifierCharacter(char value) {
        return char.IsLetterOrDigit(value) || value is '_' or '-';
    }

    private static BlockContext FindInnermostBlock(string source, int offset) {
        var position = Math.Min(Math.Max(offset, 0), source.Length);
        var frames = new Stack<(int HeaderStart, int OpenBrace, SourceBlockKind Kind)>();
        var best = new BlockContext(SourceBlockKind.TopLevel, 0, 0, source.Length);
        var segmentStart = 0;
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

            switch (source[index]) {
                case '{': {
                    var headerStart = SkipTrivia(source, segmentStart, index);
                    var headerEnd = TrimEnd(source, headerStart, index);
                    var header = source.AsSpan(headerStart, headerEnd - headerStart);
                    var kind = header.StartsWith("@tokens", StringComparison.OrdinalIgnoreCase)
                        ? SourceBlockKind.Tokens
                        : SourceBlockKind.Style;
                    frames.Push((headerStart, index, kind));
                    segmentStart = index + 1;
                    break;
                }
                case '}':
                    if (frames.Count > 0) {
                        var frame = frames.Pop();
                        if (position >= frame.OpenBrace && position <= index &&
                            frame.OpenBrace >= (best.Kind == SourceBlockKind.TopLevel ? -1 : best.ContentStart)) {
                            best = new BlockContext(
                                frame.Kind,
                                frame.HeaderStart,
                                frame.OpenBrace + 1,
                                index);
                        }
                    }

                    segmentStart = index + 1;
                    break;
                case ';':
                    segmentStart = index + 1;
                    break;
            }

            index++;
        }

        while (frames.Count > 0) {
            var frame = frames.Pop();
            if (position >= frame.OpenBrace &&
                frame.OpenBrace >= (best.Kind == SourceBlockKind.TopLevel ? -1 : best.ContentStart)) {
                best = new BlockContext(
                    frame.Kind,
                    frame.HeaderStart,
                    frame.OpenBrace + 1,
                    source.Length);
            }
        }

        return best;
    }

    private static SourceStatement StatementAt(
        string source,
        BlockContext block,
        int offset) {
        var position = Math.Min(Math.Max(offset, block.ContentStart), block.End);
        var depth = 0;
        var start = block.ContentStart;
        var index = block.ContentStart;
        while (index < position) {
            if (StartsComment(source, index)) {
                index = SkipComment(source, index);
                continue;
            }

            if (source[index] is '\'' or '"') {
                index = SkipString(source, index);
                continue;
            }

            switch (source[index]) {
                case '{':
                    depth++;
                    break;
                case '}':
                    if (depth == 0) {
                        start = index + 1;
                    } else {
                        depth--;
                    }

                    break;
                case ';' when depth == 0:
                    start = index + 1;
                    break;
            }

            index++;
        }

        start = SkipTrivia(source, start, position);
        var end = position;
        depth = 0;
        index = position;
        var colon = -1;
        var equal = -1;
        index = start;
        while (index < block.End) {
            if (StartsComment(source, index)) {
                index = SkipComment(source, index);
                continue;
            }

            if (source[index] is '\'' or '"') {
                index = SkipString(source, index);
                continue;
            }

            var character = source[index];
            if (character == '(') {
                depth++;
            } else if (character == ')') {
                depth = Math.Max(0, depth - 1);
            } else if (character == ':' && depth == 0 && colon < 0) {
                colon = index;
            } else if (character == '=' && depth == 0 && equal < 0) {
                equal = index;
            } else if (depth == 0 && character is ';' or '}' or '{') {
                end = index;
                break;
            }

            index++;
        }

        end = TrimEnd(source, start, Math.Min(block.End, end));
        return new SourceStatement(
            start,
            end,
            colon >= start && colon < end ? colon : -1,
            equal >= start && equal < end ? equal : -1,
            DepthZeroCharacterBefore(source, colon, end, '{') >= 0);
    }

    private static bool StartsComment(string source, int index) {
        return index >= 0 && index + 1 < source.Length &&
               source[index] == '/' && source[index + 1] == '*';
    }

    private static int SkipComment(string source, int index) {
        index += 2;
        while (index + 1 < source.Length &&
               !(source[index] == '*' && source[index + 1] == '/')) {
            index++;
        }

        return Math.Min(source.Length, index + 2);
    }

    private static int SkipString(string source, int index) {
        var quote = source[index++];
        while (index < source.Length) {
            if (source[index] == '\\' && index + 1 < source.Length) {
                index += 2;
            } else if (source[index++] == quote) {
                break;
            }
        }

        return index;
    }

    private static int SkipTrivia(string source, int start, int end) {
        var index = Math.Max(0, start);
        end = Math.Min(source.Length, end);
        while (index < end) {
            if (char.IsWhiteSpace(source[index])) {
                index++;
                continue;
            }

            if (!StartsComment(source, index)) {
                break;
            }

            index = SkipComment(source, index);
        }

        return Math.Min(index, end);
    }

    private static int TrimEnd(string source, int start, int end) {
        var index = Math.Min(Math.Max(start, 0), source.Length);
        end = Math.Min(Math.Max(index, end), source.Length);
        while (end > index && char.IsWhiteSpace(source[end - 1])) {
            end--;
        }

        return end;
    }

    private static int DepthZeroCharacterBefore(
        string source,
        int start,
        int end,
        char expected) {
        if (start < 0) {
            return -1;
        }

        var depth = 0;
        var index = start;
        while (index < end) {
            if (StartsComment(source, index)) {
                index = SkipComment(source, index);
                continue;
            }

            if (source[index] is '\'' or '"') {
                index = SkipString(source, index);
                continue;
            }

            if (source[index] == '(') {
                depth++;
            } else if (source[index] == ')') {
                depth = Math.Max(0, depth - 1);
            } else if (depth == 0 && source[index] == expected) {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static IEnumerable<SourceWord> Identifiers(string source, int start, int end) {
        var index = Math.Min(Math.Max(start, 0), source.Length);
        end = Math.Min(Math.Max(index, end), source.Length);
        while (index < end) {
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

            var wordStart = index++;
            while (index < end && IsIdentifierCharacter(source[index])) {
                index++;
            }

            yield return new SourceWord(wordStart, index - wordStart);
        }
    }
}
