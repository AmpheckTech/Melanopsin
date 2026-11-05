// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal sealed class BidiParagraph {
    private readonly BidiCharacter[] _characters;

    public BidiParagraph(
        int start,
        int length,
        TextDirection direction,
        int embeddingLevel,
        BidiCharacter[] characters) {
        Start = start;
        Length = length;
        Direction = direction;
        EmbeddingLevel = embeddingLevel;
        _characters = characters;
    }

    public int Start { get; }

    public int Length { get; }

    public TextDirection Direction { get; }

    public int EmbeddingLevel { get; }

    public int LevelAt(int utf16Offset) {
        var relative = utf16Offset - Start;
        var low = 0;
        var high = _characters.Length - 1;
        while (low <= high) {
            var middle = (low + high) >> 1;
            var character = _characters[middle];
            if (relative < character.Utf16Start) {
                high = middle - 1;
            } else if (relative >= character.Utf16Start + character.Utf16Length) {
                low = middle + 1;
            } else {
                return character.Level;
            }
        }

        return EmbeddingLevel;
    }
}
