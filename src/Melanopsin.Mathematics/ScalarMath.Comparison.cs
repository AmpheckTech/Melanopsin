// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;

namespace Melanopsin.Mathematics;

public static partial class ScalarMath {
    public static bool IsFinite<T>(T value)
        where T : IFloatingPointIeee754<T> {
        return T.IsFinite(value);
    }

    public static bool IsNearlyZero(
        double value,
        double tolerance = DefaultTolerance) {
        ValidateTolerance(tolerance);
        return double.IsFinite(value) && Math.Abs(value) <= tolerance;
    }

    public static bool IsNearlyZero(
        float value,
        float tolerance = DefaultSingleTolerance) {
        ValidateTolerance(tolerance);
        return float.IsFinite(value) && MathF.Abs(value) <= tolerance;
    }

    public static bool IsNearlyZero<T>(T value, T tolerance)
        where T : IFloatingPointIeee754<T> {
        ValidateTolerance(tolerance);
        return T.IsFinite(value) && T.Abs(value) <= tolerance;
    }

    /// <summary>
    /// Compares two values using combined absolute and relative tolerance.
    /// </summary>
    public static bool IsNearlyEqual(
        double left,
        double right,
        double absoluteTolerance = DefaultTolerance,
        double relativeTolerance = DefaultTolerance) {
        ValidateTolerance(absoluteTolerance);
        ValidateTolerance(relativeTolerance);
        if (!double.IsFinite(left) || !double.IsFinite(right)) {
            return left == right;
        }

        var difference = Math.Abs(left - right);
        var scale = Math.Max(Math.Abs(left), Math.Abs(right));
        return difference <= Math.Max(absoluteTolerance, relativeTolerance * scale);
    }

    /// <summary>
    /// Compares two values using combined absolute and relative tolerance.
    /// </summary>
    public static bool IsNearlyEqual(
        float left,
        float right,
        float absoluteTolerance = DefaultSingleTolerance,
        float relativeTolerance = DefaultSingleTolerance) {
        ValidateTolerance(absoluteTolerance);
        ValidateTolerance(relativeTolerance);
        if (!float.IsFinite(left) || !float.IsFinite(right)) {
            return left == right;
        }

        var difference = MathF.Abs(left - right);
        var scale = MathF.Max(MathF.Abs(left), MathF.Abs(right));
        return difference <= MathF.Max(absoluteTolerance, relativeTolerance * scale);
    }

    public static bool IsNearlyEqual<T>(
        T left,
        T right,
        T absoluteTolerance,
        T relativeTolerance)
        where T : IFloatingPointIeee754<T> {
        ValidateTolerance(absoluteTolerance);
        ValidateTolerance(relativeTolerance);
        if (!T.IsFinite(left) || !T.IsFinite(right)) {
            return left == right;
        }

        var difference = T.Abs(left - right);
        var scale = T.Max(T.Abs(left), T.Abs(right));
        return difference <= T.Max(absoluteTolerance, relativeTolerance * scale);
    }

    /// <summary>
    /// Compares two values using an absolute tolerance.
    /// </summary>
    public static bool ApproximatelyEquals(
        double left,
        double right,
        double tolerance = DefaultTolerance) {
        ValidateTolerance(tolerance);
        return IsNearlyZero(left - right, tolerance);
    }

    /// <summary>
    /// Compares two values using an absolute tolerance.
    /// </summary>
    public static bool ApproximatelyEquals(
        float left,
        float right,
        float tolerance = DefaultSingleTolerance) {
        ValidateTolerance(tolerance);
        return IsNearlyZero(left - right, tolerance);
    }

    public static bool ApproximatelyEquals<T>(
        T left,
        T right,
        T tolerance)
        where T : IFloatingPointIeee754<T> {
        ValidateTolerance(tolerance);
        return IsNearlyZero(left - right, tolerance);
    }

    public static bool ApproximatelyEquals(
        Vector2 left,
        Vector2 right,
        float tolerance = DefaultSingleTolerance) {
        return IsNearlyEqual(left.X, right.X, tolerance, tolerance) &&
            IsNearlyEqual(left.Y, right.Y, tolerance, tolerance);
    }

    public static bool ApproximatelyEquals(
        Vector3 left,
        Vector3 right,
        float tolerance = DefaultSingleTolerance) {
        return IsNearlyEqual(left.X, right.X, tolerance, tolerance) &&
            IsNearlyEqual(left.Y, right.Y, tolerance, tolerance) &&
            IsNearlyEqual(left.Z, right.Z, tolerance, tolerance);
    }

    public static bool ApproximatelyEquals(
        Vector4 left,
        Vector4 right,
        float tolerance = DefaultSingleTolerance) {
        return IsNearlyEqual(left.X, right.X, tolerance, tolerance) &&
            IsNearlyEqual(left.Y, right.Y, tolerance, tolerance) &&
            IsNearlyEqual(left.Z, right.Z, tolerance, tolerance) &&
            IsNearlyEqual(left.W, right.W, tolerance, tolerance);
    }

    private static void ValidateTolerance(double tolerance) {
        if (!double.IsFinite(tolerance) || tolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
    }

    private static void ValidateTolerance(float tolerance) {
        if (!float.IsFinite(tolerance) || tolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
    }

    private static void ValidateTolerance<T>(T tolerance)
        where T : IFloatingPointIeee754<T> {
        if (!T.IsFinite(tolerance) || tolerance < T.Zero) {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
    }
}
