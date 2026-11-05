// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    /// <summary>
    /// Computes the spherical Bessel function of the first kind
    /// <c>j_n(x)</c> for a non-negative integer order.
    /// </summary>
    /// <remarks>
    /// Small and moderate arguments use a direct power series. Larger
    /// arguments use the stable upward recurrence from <c>j_0</c> and
    /// <c>j_1</c>. Negative arguments follow the parity relation
    /// <c>j_n(-x) = (-1)^n j_n(x)</c>.
    /// </remarks>
    public static double SphericalBesselJ(int order, double value) {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        if (value == 0) {
            return order == 0 ? 1 : 0;
        }

        var magnitude = Math.Abs(value);
        double result;
        if (magnitude <= order + 8) {
            result = SphericalBesselJSeries(order, magnitude);
        } else {
            var current = SphericalBesselJ0(magnitude);
            if (order == 0) {
                result = current;
            } else {
                var next = SphericalBesselJ1(magnitude);
                for (var index = 1; index < order; index++) {
                    var following =
                        (((2 * index) + 1) / magnitude * next) - current;
                    current = next;
                    next = following;
                }

                result = next;
            }
        }

        return value < 0 && (order & 1) != 0 ? -result : result;
    }

    /// <summary>Computes <c>j_0(x) = sin(x) / x</c>.</summary>
    public static double SphericalBesselJ0(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        if (value == 0) {
            return 1;
        }

        return Math.Sin(value) / value;
    }

    /// <summary>Computes <c>j_1(x) = sin(x) / x^2 - cos(x) / x</c>.</summary>
    public static double SphericalBesselJ1(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        if (value == 0) {
            return 0;
        }

        if (Math.Abs(value) < 0.1) {
            var squared = value * value;
            return value * (1.0 / 3.0 +
                (squared * (-1.0 / 30.0 +
                    (squared * (1.0 / 840.0 +
                        (squared * (-1.0 / 45360.0)))))));
        }

        var sine = Math.Sin(value);
        var cosine = Math.Cos(value);
        return (sine / (value * value)) - (cosine / value);
    }

    /// <summary>
    /// Computes the spherical Bessel function of the second kind
    /// <c>y_n(x)</c> for a non-negative integer order.
    /// </summary>
    /// <remarks>
    /// The function has a pole at zero and returns NaN there. Negative
    /// arguments follow <c>y_n(-x) = (-1)^(n+1) y_n(x)</c>.
    /// </remarks>
    public static double SphericalBesselY(int order, double value) {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        var magnitude = Math.Abs(value);
        var current = SphericalBesselY0(magnitude);
        if (order == 0) {
            return value < 0 ? -current : current;
        }

        var next = SphericalBesselY1(magnitude);
        for (var index = 1; index < order; index++) {
            var following =
                (((2 * index) + 1) / magnitude * next) - current;
            current = next;
            next = following;
        }

        return value < 0 && (order & 1) == 0 ? -next : next;
    }

    /// <summary>Computes <c>y_0(x) = -cos(x) / x</c>.</summary>
    public static double SphericalBesselY0(double value) {
        if (double.IsNaN(value) || value == 0) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        return -Math.Cos(value) / value;
    }

    /// <summary>
    /// Computes <c>y_1(x) = -cos(x) / x^2 - sin(x) / x</c>.
    /// </summary>
    public static double SphericalBesselY1(double value) {
        if (double.IsNaN(value) || value == 0) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        return (-Math.Cos(value) / (value * value)) -
            (Math.Sin(value) / value);
    }

    private static double SphericalBesselJSeries(int order, double magnitude) {
        var squared = magnitude * magnitude;
        var term = 1.0;
        var sum = 1.0;
        for (var index = 1; index < MaximumIterations; index++) {
            term *= -squared /
                (2.0 * index * ((2 * order) + (2 * index) + 1));
            sum += term;
            if (Math.Abs(term) <= RelativeAccuracy * Math.Abs(sum)) {
                break;
            }
        }

        var leading = Math.Pow(magnitude, order);
        for (var index = 1; index <= order; index++) {
            leading /= (2 * index) + 1;
        }

        return leading * sum;
    }
}
