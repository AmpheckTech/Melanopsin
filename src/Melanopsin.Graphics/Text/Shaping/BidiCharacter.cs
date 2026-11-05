// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal struct BidiCharacter {
    public BidiCharacter(
        int codePoint,
        int utf16Start,
        int utf16Length,
        BidiClass originalClass) {
        CodePoint = codePoint;
        Utf16Start = utf16Start;
        Utf16Length = utf16Length;
        OriginalClass = originalClass;
        Class = originalClass;
        Level = 0;
    }

    public int CodePoint { get; }

    public int Utf16Start { get; }

    public int Utf16Length { get; }

    public BidiClass OriginalClass { get; }

    public BidiClass Class { get; set; }

    public byte Level { get; set; }

    public bool IsRemoved => Class is
        BidiClass.LRE or
        BidiClass.RLE or
        BidiClass.LRO or
        BidiClass.RLO or
        BidiClass.PDF or
        BidiClass.BN;
}
