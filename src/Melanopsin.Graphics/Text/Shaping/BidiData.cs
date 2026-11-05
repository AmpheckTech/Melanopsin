// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

/// <summary>
/// Resolves the Unicode bidi properties used by UAX #9.
/// </summary>
internal static class BidiData {
    public static BidiClass ResolveClass(int codePoint) {
        return (uint)codePoint > BidiTable.LastCodePoint
            ? BidiClass.L
            : BidiTable.Class(BidiTable.Find(codePoint));
    }

    public static bool TryGetBracket(
        int codePoint,
        out int pairedCodePoint,
        out bool isOpen) {
        if ((uint)codePoint > BidiTable.LastCodePoint) {
            pairedCodePoint = 0;
            isOpen = false;
            return false;
        }

        return BidiTable.TryGetBracket(codePoint, out pairedCodePoint, out isOpen);
    }
}
