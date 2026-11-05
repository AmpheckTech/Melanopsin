// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public static partial class SpecialFunctions {
    private const double BesselSeriesBoundary = 32;
    private const double TwoOverPi = 0.636619772367581343075535053490057448;

    /// <summary>
    /// Computes the integer-order Bessel function of the first kind, J_n(x).
    /// </summary>
    public static double BesselJ(int order, double value) {
        if (order < 0) {
            ArgumentOutOfRangeException.ThrowIfEqual(order, int.MinValue);

            var magnitude = -order;
            var result = BesselJ(magnitude, value);
            return (magnitude & 1) == 0 ? result : -result;
        }

        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        if (order == 0) {
            return BesselJ0(value);
        }

        if (order == 1) {
            return BesselJ1(value);
        }

        var absoluteValue = Math.Abs(value);
        if (absoluteValue < BesselSeriesBoundary && order > absoluteValue) {
            return BesselJSeries(order, value);
        }

        var previous = BesselJ0(value);
        var current = BesselJ1(value);
        for (var index = 1; index < order; index++) {
            var next = ((2.0 * index / value) * current) - previous;
            previous = current;
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Computes the integer-order Bessel function of the second kind, Y_n(x).
    /// </summary>
    /// <remarks>
    /// The real-valued integer-order function is supported for negative
    /// arguments as well: Y_n(-x) = (-1)^n Y_n(x). The value at zero is a pole
    /// and returns negative infinity.
    /// </remarks>
    public static double BesselY(int order, double value) {
        if (order < 0) {
            ArgumentOutOfRangeException.ThrowIfEqual(order, int.MinValue);

            var magnitude = -order;
            var result = BesselY(magnitude, value);
            return (magnitude & 1) == 0 ? result : -result;
        }

        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return double.NegativeInfinity;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        var sign = value < 0 && (order & 1) != 0 ? -1.0 : 1.0;
        var absoluteValue = Math.Abs(value);
        if (order == 0) {
            return sign * BesselY0(absoluteValue);
        }

        if (order == 1) {
            return sign * BesselY1(absoluteValue);
        }

        var previous = BesselY0(absoluteValue);
        var current = BesselY1(absoluteValue);
        for (var index = 1; index < order; index++) {
            var next = ((2.0 * index / absoluteValue) * current) - previous;
            previous = current;
            current = next;
        }

        return sign * current;
    }

    public static double BesselJ0(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        var absoluteValue = Math.Abs(value);
        if (absoluteValue < BesselSeriesBoundary) {
            return BesselJSeries(0, value);
        }

        var scale = BesselAsymptoticScale(absoluteValue);
        var argument = 8 / absoluteValue;
        var phase = absoluteValue - Constants.PiOver4;
        var first = BesselAsymptoticP0(argument);
        var second = BesselAsymptoticQ0(argument);
        return scale *
            ((Math.Cos(phase) * first) - (argument * Math.Sin(phase) * second));
    }

    public static double BesselJ1(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        var absoluteValue = Math.Abs(value);
        double result;
        if (absoluteValue < BesselSeriesBoundary) {
            result = BesselJSeries(1, value);
        } else {
            var scale = BesselAsymptoticScale(absoluteValue);
            var argument = 8 / absoluteValue;
            var phase = absoluteValue - (3 * Constants.PiOver4);
            var first = BesselAsymptoticP1(argument);
            var second = BesselAsymptoticQ1(argument);
            result = scale *
                ((Math.Cos(phase) * first) - (argument * Math.Sin(phase) * second));
        }

        return value < 0 ? -result : result;
    }

    public static double BesselY0(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return double.NegativeInfinity;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        var absoluteValue = Math.Abs(value);
        double result;
        if (absoluteValue < BesselSeriesBoundary) {
            result = BesselY0Series(absoluteValue);
        } else {
            var scale = BesselAsymptoticScale(absoluteValue);
            var argument = 8 / absoluteValue;
            var phase = absoluteValue - Constants.PiOver4;
            result = scale *
                ((Math.Sin(phase) * BesselAsymptoticP0(argument)) +
                    (argument * Math.Cos(phase) * BesselAsymptoticQ0(argument)));
        }

        return result;
    }

    public static double BesselY1(double value) {
        if (double.IsNaN(value)) {
            return double.NaN;
        }

        if (value == 0) {
            return double.NegativeInfinity;
        }

        if (double.IsInfinity(value)) {
            return 0;
        }

        var sign = value < 0 ? -1.0 : 1.0;
        var absoluteValue = Math.Abs(value);
        double result;
        if (absoluteValue < BesselSeriesBoundary) {
            result = BesselY1Series(absoluteValue);
        } else {
            var scale = BesselAsymptoticScale(absoluteValue);
            var argument = 8 / absoluteValue;
            var phase = absoluteValue - (3 * Constants.PiOver4);
            result = scale *
                ((Math.Sin(phase) * BesselAsymptoticP1(argument)) +
                    (argument * Math.Cos(phase) * BesselAsymptoticQ1(argument)));
        }

        return sign * result;
    }

    private static double BesselJSeries(int order, double value) {
        var half = value * 0.5;
        var scaledSquare = half * half;
        var term = 1.0;
        for (var index = 1; index <= order; index++) {
            term *= half / index;
        }

        var sum = term;
        for (var index = 1; index < MaximumIterations; index++) {
            term *= -scaledSquare / (index * (order + index));
            sum += term;
            if (Math.Abs(term) <= Math.Abs(sum) * RelativeAccuracy) {
                return sum;
            }
        }

        return sum;
    }

    private static double BesselY0Series(double value) {
        var half = value * 0.5;
        var scaledSquare = half * half;
        var j0 = BesselJSeries(0, value);
        var logarithm = Math.Log(half) + Constants.EulerMascheroni;
        var sum = logarithm * j0;
        var term = 1.0;
        var harmonic = 0.0;
        for (var index = 1; index < MaximumIterations; index++) {
            term *= -scaledSquare / (index * index);
            harmonic += 1.0 / index;
            var contribution = -term * harmonic;
            sum += contribution;
            if (Math.Abs(contribution) <=
                Math.Max(Math.Abs(sum), 1) * RelativeAccuracy) {
                break;
            }
        }

        return 2 / Constants.Pi * sum;
    }

    private static double BesselY1Series(double value) {
        var half = value * 0.5;
        var scaledSquare = half * half;
        var j1 = BesselJSeries(1, value);
        var logarithm = Math.Log(half);
        var series = 0.0;
        var term = half;
        var harmonic = 0.0;
        for (var index = 0; index < MaximumIterations; index++) {
            if (index > 0) {
                term *= -scaledSquare / (index * (index + 1));
                harmonic += 1.0 / index;
            }

            var harmonicNext = harmonic + (1.0 / (index + 1));
            var contribution = term *
                ((-2 * Constants.EulerMascheroni) + harmonic + harmonicNext);
            series += contribution;
            if (Math.Abs(contribution) <=
                Math.Max(Math.Abs(series), 1) * RelativeAccuracy) {
                break;
            }
        }

        return (-2 / (Constants.Pi * value)) +
            (2 / Constants.Pi * logarithm * j1) -
            (series / Constants.Pi);
    }

    private static double BesselAsymptoticScale(double value) {
        return Math.Sqrt(TwoOverPi / value);
    }

    private static double BesselAsymptoticP0(double scaled) {
        var square = scaled * scaled;
        return 1 + (square *
            (-0.1098628627e-2 + (square *
                (0.2734510407e-4 + (square *
                    (-0.2073370639e-5 + (square * 0.2093887211e-6)))))));
    }

    private static double BesselAsymptoticQ0(double scaled) {
        var square = scaled * scaled;
        return -0.1562499995e-1 + (square *
            (0.1430488765e-3 + (square *
                (-0.6911147651e-5 + (square *
                    (0.7621095161e-6 - (square * 0.934935152e-7)))))));
    }

    private static double BesselAsymptoticP1(double scaled) {
        var square = scaled * scaled;
        return 1 + (square *
            (0.183105e-2 + (square *
                (-0.3516396496e-4 + (square *
                    (0.2457520174e-5 + (square * -0.240337019e-6)))))));
    }

    private static double BesselAsymptoticQ1(double scaled) {
        var square = scaled * scaled;
        return 0.04687499995 + (square *
            (-0.2002690873e-3 + (square *
                (0.8449199096e-5 + (square *
                    (-0.88228987e-6 + (square * 0.105787412e-6)))))));
    }
}
