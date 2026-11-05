// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;

namespace Melanopsin.UI.Platform.Win32.Windowing;

internal static class Win32MessageValue {
    public static int LowWord(IntPtr value) {
        return unchecked((short)((long)value & 0xFFFF));
    }

    public static int HighWord(IntPtr value) {
        return unchecked((short)(((long)value >> 16) & 0xFFFF));
    }

    public static Point PointFromLParam(IntPtr lParam) {
        return new Point {
            X = LowWord(lParam),
            Y = HighWord(lParam),
        };
    }
}
