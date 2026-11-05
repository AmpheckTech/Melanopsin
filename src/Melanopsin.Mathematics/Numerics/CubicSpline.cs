// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Selects the endpoint constraints of a cubic spline.
/// </summary>
public enum CubicSplineBoundaryCondition {
    /// <summary>
    /// The second derivative is zero at both endpoints.
    /// </summary>
    Natural,

    /// <summary>
    /// The first derivative is specified at both endpoints.
    /// </summary>
    Clamped,
}

/// <summary>
/// An immutable piecewise cubic interpolating spline with natural or clamped
/// endpoint constraints.
/// </summary>
public sealed class CubicSpline {
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _secondDerivatives;

    public CubicSpline(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        CubicSplineBoundaryCondition boundaryCondition =
            CubicSplineBoundaryCondition.Natural,
        double leftDerivative = 0,
        double rightDerivative = 0) {
        if (x.Length < 2) {
            throw new ArgumentException(
                "At least two knots are required.",
                nameof(x));
        }

        if (y.Length < x.Length) {
            throw new ArgumentException(
                "The value sample is shorter than the abscissa sample.",
                nameof(y));
        }

        _x = x[..x.Length].ToArray();
        _y = y[..x.Length].ToArray();
        _secondDerivatives = new double[x.Length];
        ValidateInputs(
            x,
            y,
            boundaryCondition,
            leftDerivative,
            rightDerivative);
        ComputeSecondDerivatives(
            boundaryCondition,
            leftDerivative,
            rightDerivative);
    }

    public int IntervalCount => _x.Length - 1;

    public double LeftEndpoint => _x[0];

    public double RightEndpoint => _x[^1];

    public double Evaluate(double value) {
        var interval = FindInterval(value);
        var h = _x[interval + 1] - _x[interval];
        var t = (value - _x[interval]) / h;
        var inverse = 1 - t;
        return (inverse * _y[interval]) +
            (t * _y[interval + 1]) +
            (((inverse * inverse * inverse) - inverse) *
                _secondDerivatives[interval] * h * h / 6) +
            (((t * t * t) - t) *
                _secondDerivatives[interval + 1] * h * h / 6);
    }

    public double Derivative(double value) {
        var interval = FindInterval(value);
        var h = _x[interval + 1] - _x[interval];
        var t = (value - _x[interval]) / h;
        var inverse = 1 - t;
        return ((_y[interval + 1] - _y[interval]) / h) +
            (((((3 * t * t) - 1) * _secondDerivatives[interval + 1]) -
                (((3 * inverse * inverse) - 1) * _secondDerivatives[interval])) *
                h / 6);
    }

    public double Integral(double lower, double upper) {
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) {
            throw new ArgumentOutOfRangeException(nameof(lower));
        }

        if (lower == upper) {
            return 0;
        }

        if (lower > upper) {
            return -Integral(upper, lower);
        }

        var result = 0.0;
        for (var interval = 0; interval < IntervalCount; interval++) {
            var intervalLeft = _x[interval];
            var intervalRight = _x[interval + 1];
            if (upper <= intervalLeft) {
                break;
            }

            if (lower >= intervalRight) {
                continue;
            }

            var left = Math.Max(lower, intervalLeft);
            var right = Math.Min(upper, intervalRight);
            result += IntegrateInterval(interval, left, right);
        }

        return result;
    }

    private double IntegrateInterval(int interval, double lower, double upper) {
        var h = _x[interval + 1] - _x[interval];
        var lowerT = (lower - _x[interval]) / h;
        var upperT = (upper - _x[interval]) / h;
        return h * (Primitive(interval, upperT) - Primitive(interval, lowerT));
    }

    private double Primitive(int interval, double t) {
        var t2 = t * t;
        var t4 = t2 * t2;
        var inverse = 1 - t;
        var inverseSquared = inverse * inverse;
        return (_y[interval] * t) +
            (0.5 * (_y[interval + 1] - _y[interval]) * t2) +
            (_secondDerivatives[interval] * hSquared(interval) / 6 *
                (-(inverseSquared * inverseSquared / 4) +
                    (inverseSquared * 0.5) - 0.25)) +
            (_secondDerivatives[interval + 1] * hSquared(interval) / 6 *
                ((t4 / 4) - (t2 * 0.5)));
    }

    private double hSquared(int interval) {
        var h = _x[interval + 1] - _x[interval];
        return h * h;
    }

    private int FindInterval(double value) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (value <= _x[0]) {
            return 0;
        }

        if (value >= _x[^1]) {
            return IntervalCount - 1;
        }

        var low = 0;
        var high = IntervalCount - 1;
        while (low <= high) {
            var middle = low + ((high - low) / 2);
            if (value < _x[middle]) {
                high = middle - 1;
            } else if (value > _x[middle + 1]) {
                low = middle + 1;
            } else {
                return middle;
            }
        }

        return low;
    }

    private void ComputeSecondDerivatives(
        CubicSplineBoundaryCondition boundaryCondition,
        double leftDerivative,
        double rightDerivative) {
        var count = _x.Length;
        if (count == 2) {
            return;
        }

        var diagonal = new double[count];
        var upper = new double[count];
        var rightHandSide = new double[count];
        for (var index = 1; index < count - 1; index++) {
            var leftStep = _x[index] - _x[index - 1];
            var rightStep = _x[index + 1] - _x[index];
            diagonal[index] = 2 * (leftStep + rightStep);
            upper[index] = rightStep;
            rightHandSide[index] = 6 *
                (((_y[index + 1] - _y[index]) / rightStep) -
                ((_y[index] - _y[index - 1]) / leftStep));
        }

        if (boundaryCondition == CubicSplineBoundaryCondition.Natural) {
            diagonal[0] = 1;
            diagonal[^1] = 1;
        } else {
            var firstStep = _x[1] - _x[0];
            diagonal[0] = 2 * firstStep;
            upper[0] = firstStep;
            rightHandSide[0] = 6 *
                (((_y[1] - _y[0]) / firstStep) - leftDerivative);
            var lastStep = _x[^1] - _x[^2];
            diagonal[^1] = 2 * lastStep;
            rightHandSide[^1] = 6 *
                (rightDerivative - ((_y[^1] - _y[^2]) / lastStep));
        }

        for (var index = 1; index < count; index++) {
            var factor =
                boundaryCondition == CubicSplineBoundaryCondition.Natural &&
                index == count - 1
                    ? 0
                    : (_x[index] - _x[index - 1]) / diagonal[index - 1];
            diagonal[index] -= factor * upper[index - 1];
            rightHandSide[index] -= factor * rightHandSide[index - 1];
        }

        _secondDerivatives[^1] = rightHandSide[^1] / diagonal[^1];
        for (var index = count - 2; index >= 1; index--) {
            _secondDerivatives[index] =
                (rightHandSide[index] -
                    (upper[index] * _secondDerivatives[index + 1])) /
                diagonal[index];
        }

        _secondDerivatives[0] = 0;
        if (boundaryCondition == CubicSplineBoundaryCondition.Clamped) {
            _secondDerivatives[0] =
                (rightHandSide[0] -
                    (upper[0] * _secondDerivatives[1])) /
                diagonal[0];
        }
    }

    private static void ValidateInputs(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        CubicSplineBoundaryCondition boundaryCondition,
        double leftDerivative,
        double rightDerivative) {
        if (!Enum.IsDefined(boundaryCondition)) {
            throw new ArgumentOutOfRangeException(nameof(boundaryCondition));
        }

        for (var index = 0; index < x.Length; index++) {
            if (!double.IsFinite(x[index]) || !double.IsFinite(y[index])) {
                throw new ArgumentException(
                    "Spline knots and values must be finite.",
                    nameof(x));
            }

            if (index > 0 && x[index] <= x[index - 1]) {
                throw new ArgumentException(
                    "Spline knots must be strictly increasing.",
                    nameof(x));
            }
        }

        if (boundaryCondition == CubicSplineBoundaryCondition.Clamped) {
            if (!double.IsFinite(leftDerivative)) {
                throw new ArgumentOutOfRangeException(nameof(leftDerivative));
            }

            if (!double.IsFinite(rightDerivative)) {
                throw new ArgumentOutOfRangeException(nameof(rightDerivative));
            }
        }
    }
}
