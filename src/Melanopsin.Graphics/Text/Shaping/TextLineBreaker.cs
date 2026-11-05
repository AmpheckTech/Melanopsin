// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Text;

namespace Melanopsin.Graphics.Text;

/// <summary>
/// Splits text into the segments a line may be wrapped between, following the
/// default Unicode line breaking algorithm, UAX #14.
/// </summary>
/// <remarks>
/// Applies the default rules of UAX #14 revision 56 (Unicode 17.0) without
/// tailoring. This type only walks paragraphs and emits tokens; units come from
/// <see cref="LineBreakContext"/> and the rules from
/// <see cref="LineBreakRules"/>.
/// </remarks>
internal static class TextLineBreaker {
    /// <summary>
    /// Splits <paramref name="text"/> into tokens. Tokens that start with a
    /// space are flagged as whitespace, and hard line breaks are yielded as
    /// their own tokens.
    /// </summary>
    public static IEnumerable<TextToken> Tokenize(string text) {
        var paragraphStart = 0;
        var index = 0;
        while (index < text.Length) {
            var runeStatus = Rune.DecodeFromUtf16(
                text.AsSpan(index),
                out var rune,
                out var consumed);
            if (runeStatus != OperationStatus.Done) {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            var lineBreakClass = LineBreakData.Resolve(rune.Value, out _);
            if (lineBreakClass is
                LineBreakClass.BK or
                LineBreakClass.CR or
                LineBreakClass.LF or
                LineBreakClass.NL) {
                foreach (var token in TokenizeParagraph(
                    text,
                    paragraphStart,
                    index)) {
                    yield return token;
                }

                // LB5: CR LF is a single hard line break.
                if (lineBreakClass == LineBreakClass.CR &&
                    index + 1 < text.Length &&
                    text[index + 1] == '\n') {
                    consumed++;
                }

                yield return new TextToken(index, consumed, false, true);
                index += consumed;
                paragraphStart = index;
                continue;
            }

            index += consumed;
        }

        foreach (var token in TokenizeParagraph(
            text,
            paragraphStart,
            text.Length)) {
            yield return token;
        }
    }

    private static List<TextToken> TokenizeParagraph(
        string text,
        int start,
        int end) {
        var tokens = new List<TextToken>();
        var length = end - start;
        if (length <= 0) {
            return tokens;
        }

        var span = text.AsSpan(start, length);
        var context = new LineBreakContext(span);
        var tokenStart = 0;
        for (var index = 0; index + 1 < context.Count; index++) {
            if (!LineBreakRules.IsBreakAllowed(context, index)) {
                continue;
            }

            var breakOffset = context.EndOf(index);
            AppendToken(tokens, span, start, tokenStart, breakOffset);
            tokenStart = breakOffset;
        }

        // LB3: text always ends with a break opportunity.
        AppendToken(tokens, span, start, tokenStart, length);
        return tokens;
    }

    private static void AppendToken(
        List<TextToken> tokens,
        ReadOnlySpan<char> span,
        int start,
        int tokenStart,
        int tokenEnd) {
        if (tokenEnd <= tokenStart) {
            return;
        }

        var segment = span[tokenStart..tokenEnd];
        var status = Rune.DecodeFromUtf16(segment, out var first, out _);
        tokens.Add(new TextToken(
            start + tokenStart,
            tokenEnd - tokenStart,
            status == OperationStatus.Done && Rune.IsWhiteSpace(first),
            false));
    }
}
