// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

/// <summary>
/// Resolves the Unicode <c>Line_Break</c> property value and the additional
/// properties that UAX #14 rules consult for a code point.
/// </summary>
/// <remarks>
/// Code points that <c>LineBreak.txt</c> does not list report <c>XX</c>; the
/// documented <c>ID</c> and <c>PR</c> defaults are stored as explicit ranges in
/// <see cref="LineBreakTable"/>.
/// </remarks>
internal static class LineBreakData {
    /// <summary>
    /// Returns the <c>Line_Break</c> property value and the extra properties of
    /// <paramref name="codePoint"/>. Code points outside the Unicode code space
    /// report <c>XX</c> and no extra properties.
    /// </summary>
    public static LineBreakClass Resolve(int codePoint, out LineBreakFlags flags) {
        if ((uint)codePoint > LineBreakTable.LastCodePoint) {
            flags = LineBreakFlags.None;
            return LineBreakClass.XX;
        }

        var row = LineBreakTable.Find(codePoint);
        flags = LineBreakTable.Flags(row);
        return LineBreakTable.Class(row);
    }
}
