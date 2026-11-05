// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal sealed class BidiText {
    private readonly BidiParagraph[] _paragraphs;

    public BidiText(BidiParagraph[] paragraphs, TextDirection requested) {
        _paragraphs = paragraphs;
        BaseDirection = paragraphs.Length == 0
            ? requested == TextDirection.RightToLeft
                ? TextDirection.RightToLeft
                : TextDirection.LeftToRight
            : paragraphs[0].Direction;
    }

    public TextDirection BaseDirection { get; }

    public int LevelAt(int utf16Offset) {
        var low = 0;
        var high = _paragraphs.Length - 1;
        while (low <= high) {
            var middle = (low + high) >> 1;
            var paragraph = _paragraphs[middle];
            if (utf16Offset < paragraph.Start) {
                high = middle - 1;
            } else if (utf16Offset >= paragraph.Start + paragraph.Length) {
                low = middle + 1;
            } else {
                return paragraph.LevelAt(utf16Offset);
            }
        }

        return BaseDirection == TextDirection.RightToLeft ? 1 : 0;
    }

    public TextDirection DirectionAt(int utf16Offset) {
        return (LevelAt(utf16Offset) & 1) == 0
            ? TextDirection.LeftToRight
            : TextDirection.RightToLeft;
    }
}
