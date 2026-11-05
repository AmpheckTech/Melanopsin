// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A closed finite real interval <c>[Lower, Upper]</c>.
/// </summary>
/// <remarks>
/// Arithmetic is outward-safe at the level of IEEE results but the default
/// representation deliberately excludes NaN and infinite endpoints. Methods
/// whose mathematical result is not a finite interval throw or return
/// <see langword="false"/> through their <c>Try</c> form.
/// </remarks>
public readonly record struct Interval {
    public Interval(double lower, double upper) {
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) {
            throw new ArgumentOutOfRangeException(
                nameof(lower),
                "Interval endpoints must be finite.");
        }

        if (lower > upper) {
            throw new ArgumentOutOfRangeException(
                nameof(upper),
                "The lower endpoint must not exceed the upper endpoint.");
        }

        Lower = lower;
        Upper = upper;
    }

    public double Lower { get; }

    public double Upper { get; }

    public double Width => Upper - Lower;

    public double Midpoint => Lower + ((Upper - Lower) * 0.5);

    public double Radius => Width * 0.5;

    public bool IsDegenerate => Lower == Upper;

    public bool Contains(double value) {
        return value >= Lower && value <= Upper;
    }

    public bool ContainsStrict(double value) {
        return value > Lower && value < Upper;
    }

    public bool Contains(Interval other) {
        return other.Lower >= Lower && other.Upper <= Upper;
    }

    public bool Intersects(Interval other) {
        return Lower <= other.Upper && Upper >= other.Lower;
    }

    public bool TryIntersect(Interval other, out Interval intersection) {
        return TryIntersect(other, out intersection, out _);
    }

    /// <summary>
    /// Attempts to intersect two intervals and distinguishes an empty
    /// intersection from a numerical failure.
    /// </summary>
    public bool TryIntersect(
        Interval other,
        out Interval intersection,
        out IntervalOperationReport report) {
        if (!Intersects(other)) {
            intersection = default;
            report = new IntervalOperationReport(
                IntervalOperation.Intersection,
                IntervalStatus.EmptyIntersection);
            return false;
        }

        intersection = new Interval(
            Math.Max(Lower, other.Lower),
            Math.Min(Upper, other.Upper));
        report = new IntervalOperationReport(
            IntervalOperation.Intersection,
            IntervalStatus.Succeeded);
        return true;
    }

    public Interval Intersection(Interval other) {
        if (!TryIntersect(other, out var intersection)) {
            throw new ArithmeticException(
                "The intervals do not intersect.");
        }

        return intersection;
    }

    public Interval Hull(Interval other) {
        return new Interval(
            Math.Min(Lower, other.Lower),
            Math.Max(Upper, other.Upper));
    }

    public double Clamp(double value) {
        if (double.IsNaN(value)) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return Math.Clamp(value, Lower, Upper);
    }

    public double DistanceTo(double value) {
        if (value < Lower) {
            return Lower - value;
        }

        return value > Upper ? value - Upper : 0;
    }

    public Interval Add(Interval other) {
        return Create(Lower + other.Lower, Upper + other.Upper);
    }

    public Interval Add(double value) {
        return Create(Lower + value, Upper + value);
    }

    public Interval Subtract(Interval other) {
        return Create(Lower - other.Upper, Upper - other.Lower);
    }

    public Interval Subtract(double value) {
        return Create(Lower - value, Upper - value);
    }

    public Interval Negate() {
        return new Interval(-Upper, -Lower);
    }

    public Interval Multiply(Interval other) {
        var first = Lower * other.Lower;
        var second = Lower * other.Upper;
        var third = Upper * other.Lower;
        var fourth = Upper * other.Upper;
        return Create(
            Math.Min(Math.Min(first, second), Math.Min(third, fourth)),
            Math.Max(Math.Max(first, second), Math.Max(third, fourth)));
    }

    public Interval Scale(double factor) {
        if (!double.IsFinite(factor)) {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        return factor >= 0
            ? Create(Lower * factor, Upper * factor)
            : Create(Upper * factor, Lower * factor);
    }

    public bool TryDivide(Interval divisor, out Interval quotient) {
        return TryDivide(divisor, out quotient, out _);
    }

    /// <summary>
    /// Attempts interval division and reports a divisor containing zero or a
    /// non-finite endpoint caused by overflow.
    /// </summary>
    public bool TryDivide(
        Interval divisor,
        out Interval quotient,
        out IntervalOperationReport report) {
        if (divisor.Contains(0)) {
            quotient = default;
            report = new IntervalOperationReport(
                IntervalOperation.Division,
                IntervalStatus.DivisorContainsZero);
            return false;
        }

        var reciprocalLower = 1 / divisor.Upper;
        var reciprocalUpper = 1 / divisor.Lower;
        if (!double.IsFinite(reciprocalLower) ||
            !double.IsFinite(reciprocalUpper)) {
            quotient = default;
            report = new IntervalOperationReport(
                IntervalOperation.Division,
                IntervalStatus.NonFiniteOutput);
            return false;
        }

        var first = Lower * reciprocalLower;
        var second = Lower * reciprocalUpper;
        var third = Upper * reciprocalLower;
        var fourth = Upper * reciprocalUpper;
        var lower = Math.Min(Math.Min(first, second), Math.Min(third, fourth));
        var upper = Math.Max(Math.Max(first, second), Math.Max(third, fourth));
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) {
            quotient = default;
            report = new IntervalOperationReport(
                IntervalOperation.Division,
                IntervalStatus.NonFiniteOutput);
            return false;
        }

        quotient = new Interval(lower, upper);
        report = new IntervalOperationReport(
            IntervalOperation.Division,
            IntervalStatus.Succeeded);
        return true;
    }

    public Interval Divide(Interval divisor) {
        if (!TryDivide(divisor, out var quotient)) {
            throw new DivideByZeroException(
                "Interval division requires a divisor that excludes zero.");
        }

        return quotient;
    }

    public Interval Reciprocal() {
        if (Contains(0)) {
            throw new DivideByZeroException(
                "An interval containing zero has no finite reciprocal.");
        }

        return new Interval(1 / Upper, 1 / Lower);
    }

    public Interval Abs() {
        if (Contains(0)) {
            return new Interval(0, Math.Max(-Lower, Upper));
        }

        return Lower >= 0
            ? this
            : new Interval(-Upper, -Lower);
    }

    public Interval Square() {
        if (Contains(0)) {
            return new Interval(0, Math.Max(Lower * Lower, Upper * Upper));
        }

        return Lower >= 0
            ? new Interval(Lower * Lower, Upper * Upper)
            : new Interval(Upper * Upper, Lower * Lower);
    }

    /// <summary>
    /// Raises the interval to a non-negative integer power.
    /// </summary>
    public Interval Pow(int exponent) {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        if (exponent == 0) {
            return new Interval(1, 1);
        }

        var result = new Interval(1, 1);
        var factor = this;
        var remaining = exponent;
        while (remaining > 0) {
            if ((remaining & 1) != 0) {
                result = result.Multiply(factor);
            }

            remaining >>= 1;
            if (remaining > 0) {
                factor = factor.Square();
            }
        }

        return result;
    }

    public bool TrySqrt(out Interval result) {
        return TrySqrt(out result, out _);
    }

    /// <summary>
    /// Attempts the real square root of an interval and reports a negative domain.
    /// </summary>
    public bool TrySqrt(
        out Interval result,
        out IntervalOperationReport report) {
        if (Upper < 0) {
            result = default;
            report = new IntervalOperationReport(
                IntervalOperation.SquareRoot,
                IntervalStatus.OutsideDomain);
            return false;
        }

        result = new Interval(Math.Sqrt(Math.Max(0, Lower)), Math.Sqrt(Upper));
        report = new IntervalOperationReport(
            IntervalOperation.SquareRoot,
            IntervalStatus.Succeeded);
        return true;
    }

    public Interval Sqrt() {
        if (!TrySqrt(out var result)) {
            throw new ArithmeticException(
                "A strictly negative interval has no real square root.");
        }

        return result;
    }

    public bool TryLog(out Interval result) {
        return TryLog(out result, out _);
    }

    /// <summary>
    /// Attempts the natural logarithm of an interval and reports a non-positive
    /// domain.
    /// </summary>
    public bool TryLog(
        out Interval result,
        out IntervalOperationReport report) {
        if (Lower <= 0) {
            result = default;
            report = new IntervalOperationReport(
                IntervalOperation.Logarithm,
                IntervalStatus.OutsideDomain);
            return false;
        }

        result = new Interval(Math.Log(Lower), Math.Log(Upper));
        report = new IntervalOperationReport(
            IntervalOperation.Logarithm,
            IntervalStatus.Succeeded);
        return true;
    }

    public Interval Log() {
        if (!TryLog(out var result)) {
            throw new ArithmeticException(
                "A natural logarithm interval requires strictly positive bounds.");
        }

        return result;
    }

    public Interval Exp() {
        return Create(Math.Exp(Lower), Math.Exp(Upper));
    }

    /// <summary>
    /// Computes conservative bounds for <c>sin(x)</c> over the interval.
    /// </summary>
    public Interval Sin() {
        var minimum = Math.Min(Math.Sin(Lower), Math.Sin(Upper));
        var maximum = Math.Max(Math.Sin(Lower), Math.Sin(Upper));
        if (Width >= Constants.TwoPi) {
            return new Interval(-1, 1);
        }

        var firstIndex = Math.Ceiling((Lower - Constants.PiOver2) / Constants.Pi);
        AddExtremum(firstIndex, Constants.PiOver2, ref minimum, ref maximum);
        AddExtremum(firstIndex + 1, Constants.PiOver2, ref minimum, ref maximum);
        return new Interval(minimum, maximum);
    }

    /// <summary>
    /// Computes conservative bounds for <c>cos(x)</c> over the interval.
    /// </summary>
    public Interval Cos() {
        var minimum = Math.Min(Math.Cos(Lower), Math.Cos(Upper));
        var maximum = Math.Max(Math.Cos(Lower), Math.Cos(Upper));
        if (Width >= Constants.TwoPi) {
            return new Interval(-1, 1);
        }

        var firstIndex = Math.Ceiling(Lower / Constants.Pi);
        AddExtremum(firstIndex, 0, ref minimum, ref maximum);
        AddExtremum(firstIndex + 1, 0, ref minimum, ref maximum);
        return new Interval(minimum, maximum);
    }

    public static Interval operator +(Interval left, Interval right) {
        return left.Add(right);
    }

    public static Interval operator +(Interval left, double right) {
        return left.Add(right);
    }

    public static Interval operator -(Interval left, Interval right) {
        return left.Subtract(right);
    }

    public static Interval operator -(Interval value) {
        return value.Negate();
    }

    public static Interval operator *(Interval left, Interval right) {
        return left.Multiply(right);
    }

    public static Interval operator *(Interval left, double right) {
        return left.Scale(right);
    }

    public static Interval operator /(Interval left, Interval right) {
        return left.Divide(right);
    }

    private void AddExtremum(
        double index,
        double phase,
        ref double minimum,
        ref double maximum) {
        var point = (index * Constants.Pi) + phase;
        if (point < Lower || point > Upper) {
            return;
        }

        if (Math.IEEERemainder(index, 2) == 0) {
            maximum = 1;
        } else {
            minimum = -1;
        }
    }

    private static Interval Create(double lower, double upper) {
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) {
            throw new ArithmeticException(
                "The interval operation produced a non-finite endpoint.");
        }

        return new Interval(lower, upper);
    }
}
