// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Backend.Skia;

internal static class SkiaTextureCapacity {
    public static int Get(int dimension) {
        if (dimension <= 0) {
            throw new ArgumentOutOfRangeException(nameof(dimension));
        }

        var capacity = (long)dimension + Math.Max(64, dimension / 8L);
        capacity = (capacity + 255) & ~255L;
        return capacity > int.MaxValue
            ? int.MaxValue
            : (int)capacity;
    }
}
