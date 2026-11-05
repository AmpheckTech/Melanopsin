// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;

namespace Melanopsin.Mathematics;

public static partial class ScalarMath {
    public static double Clamp(double value, double minimum, double maximum) {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);
        if (value < minimum) {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }

    public static float Clamp(float value, float minimum, float maximum) {
        return float.Clamp(value, minimum, maximum);
    }

    public static T Clamp<T>(T value, T minimum, T maximum)
        where T : INumber<T> {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);
        if (value < minimum) {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }

    public static T Min<T>(T left, T right)
        where T : INumber<T> {
        return T.Min(left, right);
    }

    public static T Max<T>(T left, T right)
        where T : INumber<T> {
        return T.Max(left, right);
    }

    public static double Clamp01(double value) {
        return Clamp(value, 0, 1);
    }

    public static float Clamp01(float value) {
        return Clamp(value, 0, 1);
    }

    public static T Clamp01<T>(T value)
        where T : INumber<T> {
        return Clamp(value, T.Zero, T.One);
    }
}
