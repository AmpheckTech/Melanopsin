// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Styling;

/// <summary>
/// MCSS specificity represented as additive ID, class/state, and type counts.
/// Values compare lexicographically so selector length cannot overflow a weight.
/// </summary>
public readonly record struct StyleSpecificity(
    int IdCount,
    int ClassOrStateCount,
    int TypeCount) : IComparable<StyleSpecificity> {
    public static StyleSpecificity Zero => default;

    public int CompareTo(StyleSpecificity other) {
        var result = IdCount.CompareTo(other.IdCount);
        if (result != 0) {
            return result;
        }

        result = ClassOrStateCount.CompareTo(other.ClassOrStateCount);
        return result != 0 ? result : TypeCount.CompareTo(other.TypeCount);
    }

    public static bool operator <(StyleSpecificity left, StyleSpecificity right) {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(StyleSpecificity left, StyleSpecificity right) {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(StyleSpecificity left, StyleSpecificity right) {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(StyleSpecificity left, StyleSpecificity right) {
        return left.CompareTo(right) >= 0;
    }
}
