// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal static class TextDirectionResolver {
    public static TextDirection ResolveRune(
        int index,
        TextDirection baseDirection,
        BidiText bidi) {
        if (index >= 0) {
            return bidi.DirectionAt(index);
        }

        return baseDirection;
    }
}
