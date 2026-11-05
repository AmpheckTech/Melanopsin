// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal static class BidiVisualOrder {
    public static void Reorder(ReadOnlySpan<int> levels, List<int> items) {
        if (items.Count < 2) {
            return;
        }

        var highest = 0;
        var lowestOdd = int.MaxValue;
        for (var index = 0; index < items.Count; index++) {
            var level = levels[items[index]];
            highest = Math.Max(highest, level);
            if ((level & 1) != 0) {
                lowestOdd = Math.Min(lowestOdd, level);
            }
        }

        if (lowestOdd == int.MaxValue) {
            return;
        }

        for (var level = highest; level >= lowestOdd; level--) {
            for (var start = 0; start < items.Count;) {
                while (start < items.Count && levels[items[start]] < level) {
                    start++;
                }

                var end = start;
                while (end < items.Count && levels[items[end]] >= level) {
                    end++;
                }

                if (end - start > 1) {
                    items.Reverse(start, end - start);
                }

                start = end;
            }
        }
    }
}
