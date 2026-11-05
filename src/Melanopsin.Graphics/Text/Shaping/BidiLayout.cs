// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Text;

namespace Melanopsin.Graphics.Text;

/// <summary>
/// Splits text into UAX #9 paragraphs and resolves their base directions.
/// </summary>
internal static class BidiLayout {
    public static BidiText Analyze(string text, TextDirection requested) {
        ArgumentNullException.ThrowIfNull(text);
        var paragraphs = new List<BidiParagraph>();
        var start = 0;
        var index = 0;
        while (index < text.Length) {
            var status = Rune.DecodeFromUtf16(
                text.AsSpan(index),
                out var rune,
                out var consumed);
            if (status != OperationStatus.Done) {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            if (BidiData.ResolveClass(rune.Value) != BidiClass.B) {
                index += consumed;
                continue;
            }

            if (rune.Value == '\r' &&
                index + consumed < text.Length &&
                text[index + consumed] == '\n') {
                consumed++;
            }

            index += consumed;
            paragraphs.Add(BidiAlgorithm.AnalyzeParagraph(text, start, index - start, requested));
            start = index;
        }

        if (start < text.Length) {
            paragraphs.Add(BidiAlgorithm.AnalyzeParagraph(
                text,
                start,
                text.Length - start,
                requested));
        }

        return new BidiText([.. paragraphs], requested);
    }

    internal static BidiCharacter[] Decode(ReadOnlySpan<char> text) {
        var characters = new List<BidiCharacter>(text.Length);
        for (var index = 0; index < text.Length;) {
            var status = Rune.DecodeFromUtf16(text[index..], out var rune, out var consumed);
            if (status != OperationStatus.Done) {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            characters.Add(new BidiCharacter(
                rune.Value,
                index,
                consumed,
                BidiData.ResolveClass(rune.Value)));
            index += consumed;
        }

        return [.. characters];
    }
}
