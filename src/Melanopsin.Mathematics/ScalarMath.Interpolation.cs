// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;

namespace Melanopsin.Mathematics;

public static partial class ScalarMath {
    public static double Lerp(double start, double end, double amount) {
        return double.Lerp(start, end, amount);
    }

    public static float Lerp(float start, float end, float amount) {
        return float.Lerp(start, end, amount);
    }

    public static T Lerp<T>(T start, T end, T amount)
        where T : INumber<T> {
        return start + ((end - start) * amount);
    }

    public static double InverseLerp(double start, double end, double value) {
        return start == end ? 0 : (value - start) / (end - start);
    }

    public static float InverseLerp(float start, float end, float value) {
        return start == end ? 0 : (value - start) / (end - start);
    }

    public static T InverseLerp<T>(T start, T end, T value)
        where T : IFloatingPoint<T> {
        return start == end ? T.Zero : (value - start) / (end - start);
    }

    public static double Remap(
        double value,
        double fromMinimum,
        double fromMaximum,
        double toMinimum,
        double toMaximum) {
        if (fromMinimum == fromMaximum) {
            throw new ArgumentException(
                "The source range must have non-zero length.",
                nameof(fromMaximum));
        }

        return Lerp(
            toMinimum,
            toMaximum,
            InverseLerp(fromMinimum, fromMaximum, value));
    }

    public static float Remap(
        float value,
        float fromMinimum,
        float fromMaximum,
        float toMinimum,
        float toMaximum) {
        if (fromMinimum == fromMaximum) {
            throw new ArgumentException(
                "The source range must have non-zero length.",
                nameof(fromMaximum));
        }

        return Lerp(
            toMinimum,
            toMaximum,
            InverseLerp(fromMinimum, fromMaximum, value));
    }

    public static T Remap<T>(
        T value,
        T fromMinimum,
        T fromMaximum,
        T toMinimum,
        T toMaximum)
        where T : IFloatingPoint<T> {
        if (fromMinimum == fromMaximum) {
            throw new ArgumentException(
                "The source range must have non-zero length.",
                nameof(fromMaximum));
        }

        return Lerp(
            toMinimum,
            toMaximum,
            InverseLerp(fromMinimum, fromMaximum, value));
    }

    public static double SmoothStep(double edge0, double edge1, double value) {
        if (edge0 == edge1) {
            throw new ArgumentException(
                "Smooth-step edges must be different.",
                nameof(edge1));
        }

        var amount = Clamp01(InverseLerp(edge0, edge1, value));
        return amount * amount * (3 - (2 * amount));
    }

    public static float SmoothStep(float edge0, float edge1, float value) {
        if (edge0 == edge1) {
            throw new ArgumentException(
                "Smooth-step edges must be different.",
                nameof(edge1));
        }

        var amount = Clamp01(InverseLerp(edge0, edge1, value));
        return amount * amount * (3 - (2 * amount));
    }

    /// <summary>
    /// Repeats a value into the half-open interval [0, length).
    /// </summary>
    public static double Repeat(double value, double length) {
        if (!double.IsFinite(length) || length <= 0) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value - (Math.Floor(value / length) * length);
    }

    /// <summary>
    /// Repeats a value into the half-open interval [0, length).
    /// </summary>
    public static float Repeat(float value, float length) {
        if (!float.IsFinite(length) || length <= 0) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (!float.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value - (MathF.Floor(value / length) * length);
    }

    /// <summary>
    /// Maps a value through a triangle wave in [0, length].
    /// </summary>
    public static double PingPong(double value, double length) {
        if (!double.IsFinite(length) || length <= 0) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var repeated = Repeat(value, length * 2);
        return repeated <= length ? repeated : (length * 2) - repeated;
    }

    /// <summary>
    /// Maps a value through a triangle wave in [0, length].
    /// </summary>
    public static float PingPong(float value, float length) {
        if (!float.IsFinite(length) || length <= 0) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var repeated = Repeat(value, length * 2);
        return repeated <= length ? repeated : (length * 2) - repeated;
    }
}
