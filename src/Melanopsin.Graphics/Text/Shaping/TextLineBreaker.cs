using System.Text;
using Uax14Net;

namespace Melanopsin.Graphics.Text;

internal static class TextLineBreaker {
    public static IEnumerable<TextToken> Tokenize(string text) {
        var paragraphStart = 0;
        var index = 0;
        while (index < text.Length) {
            var runeStatus = Rune.DecodeFromUtf16(
                text.AsSpan(index),
                out var rune,
                out var consumed);
            if (runeStatus != System.Buffers.OperationStatus.Done) {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            if (rune.Value == '\r' || rune.Value == '\n') {
                foreach (var token in TokenizeParagraph(
                    text,
                    paragraphStart,
                    index)) {
                    yield return token;
                }

                if (rune.Value == '\r' &&
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
        if (end <= start) {
            return tokens;
        }

        var span = text.AsSpan(start, end - start);
        var tokenStart = 0;
        foreach (var opportunity in LineBreaker.Enumerate(span)) {
            if (opportunity.Position <= tokenStart) {
                continue;
            }

            var segment = span[tokenStart..opportunity.Position];
            var status = Rune.DecodeFromUtf16(segment, out var first, out _);
            tokens.Add(new TextToken(
                start + tokenStart,
                opportunity.Position - tokenStart,
                status == System.Buffers.OperationStatus.Done &&
                Rune.IsWhiteSpace(first),
                false));
            tokenStart = opportunity.Position;
        }

        if (tokenStart < span.Length) {
            tokens.Add(new TextToken(
                start + tokenStart,
                span.Length - tokenStart,
                false,
                false));
        }

        return tokens;
    }
}
