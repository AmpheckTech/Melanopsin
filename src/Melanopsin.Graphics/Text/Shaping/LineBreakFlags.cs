// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

/// <summary>
/// Additional properties of a code point that UAX #14 line breaking rules
/// consult in addition to <see cref="LineBreakClass"/>.
/// </summary>
[Flags]
internal enum LineBreakFlags : byte {
    /// <summary>No additional property applies.</summary>
    None = 0,

    /// <summary>
    /// <c>East_Asian_Width</c> is Fullwidth, Wide, or Halfwidth; the
    /// <c>$EastAsian</c> set of UAX #14.
    /// </summary>
    EastAsian = 1 << 0,

    /// <summary>
    /// <c>General_Category</c> is <c>Pi</c>. Only set for quotations.
    /// </summary>
    InitialPunctuation = 1 << 1,

    /// <summary>
    /// <c>General_Category</c> is <c>Pf</c>. Only set for quotations.
    /// </summary>
    FinalPunctuation = 1 << 2,

    /// <summary>
    /// <c>Extended_Pictographic</c> is set while <c>General_Category</c> is
    /// still <c>Cn</c>; such code points can be emoji bases.
    /// </summary>
    PotentialEmoji = 1 << 3,

    /// <summary>
    /// <c>General_Category</c> is <c>Mn</c> or <c>Mc</c>. Only set for
    /// code points whose <c>Line_Break</c> value is <c>SA</c>.
    /// </summary>
    MarkCategory = 1 << 4,
}
