// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// The outcome of an iterative one-dimensional root search.
/// </summary>
public readonly record struct RootFindResult {
    /// <summary>Initializes a root-search result.</summary>
    public RootFindResult(
        double root,
        double residual,
        int iterations,
        bool converged) {
        Root = root;
        Residual = residual;
        Iterations = iterations;
        Converged = converged;
    }

    /// <summary>Gets the accepted root estimate, or NaN when no estimate was accepted.</summary>
    public double Root { get; }

    /// <summary>Gets the function value at <see cref="Root"/>.</summary>
    public double Residual { get; }

    /// <summary>Gets the number of iteration steps.</summary>
    public int Iterations { get; }

    /// <summary>Gets whether the requested convergence test succeeded.</summary>
    public bool Converged { get; }
}

/// <summary>
/// One-dimensional root-finding algorithms for real scalar functions.
/// </summary>
/// <remarks>
/// <para>
/// All convenience methods validate caller-controlled settings and throw for
/// programming errors. The bracketing methods additionally throw when the
/// supplied interval does not enclose a sign change. Numerical termination,
/// including callback failures, zero derivatives or secant denominators,
/// non-finite trial points, and iteration exhaustion, returns
/// <see cref="RootFindResult.Converged"/> equal to <see langword="false"/>.
/// The <c>Try</c> overloads expose the concrete termination status through
/// <see cref="RootFindReport"/>.
/// </para>
/// <para>
/// A non-finite function or derivative value is a numerical termination, not
/// an invalid caller argument. Exceptions thrown by a callback itself are not
/// intercepted and remain part of the callback's own contract.
/// </para>
/// </remarks>
public static partial class RootFinding {
    private const int DefaultMaximumIterations = 100;
    private const double DefaultAbsoluteTolerance = 1e-12;
    private const double DefaultRelativeTolerance = 1e-12;

    /// <summary>Finds a root in a bracket using bisection.</summary>
    /// <exception cref="ArgumentException">
    /// The endpoints do not bracket a sign change.
    /// </exception>
    public static RootFindResult Bisection(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        TryBisection(
            function,
            lower,
            upper,
            out var result,
            out var report,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        ThrowForInvalidBracket(report);
        return result;
    }

    /// <summary>
    /// Attempts bisection and reports bracket, callback, and iteration
    /// termination conditions.
    /// </summary>
    public static bool TryBisection(
        Func<double, double> function,
        double lower,
        double upper,
        out RootFindResult result,
        out RootFindReport report,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateBracket(
            lower,
            upper,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        report = RunBisection(
            function,
            lower,
            upper,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        result = ToResult(report);
        return report.Succeeded;
    }

    /// <summary>Finds a root using Newton's method.</summary>
    /// <remarks>
    /// Numerical termination is reported by
    /// <see cref="RootFindResult.Converged"/> rather than by an exception.
    /// Use <see cref="TryNewton"/> when the concrete failure status is needed.
    /// </remarks>
    public static RootFindResult Newton(
        Func<double, double> function,
        Func<double, double> derivative,
        double initialGuess,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        TryNewton(
            function,
            derivative,
            initialGuess,
            out var result,
            out _,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        return result;
    }

    /// <summary>
    /// Attempts Newton's method and distinguishes derivative failure,
    /// non-finite trial points, and iteration exhaustion.
    /// </summary>
    public static bool TryNewton(
        Func<double, double> function,
        Func<double, double> derivative,
        double initialGuess,
        out RootFindResult result,
        out RootFindReport report,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(derivative);
        ValidateTolerancesAndIterations(
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        ValidateFiniteGuess(initialGuess, nameof(initialGuess));
        report = RunNewton(
            function,
            derivative,
            initialGuess,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        result = ToResult(report);
        return report.Succeeded;
    }

    /// <summary>Finds a root using the secant method.</summary>
    /// <remarks>
    /// Numerical termination is reported by
    /// <see cref="RootFindResult.Converged"/> rather than by an exception.
    /// Use <see cref="TrySecant"/> when the concrete failure status is needed.
    /// </remarks>
    public static RootFindResult Secant(
        Func<double, double> function,
        double firstGuess,
        double secondGuess,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        TrySecant(
            function,
            firstGuess,
            secondGuess,
            out var result,
            out _,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        return result;
    }

    /// <summary>
    /// Attempts the secant method and reports zero denominators, callback
    /// failures, non-finite trial points, and iteration exhaustion.
    /// </summary>
    public static bool TrySecant(
        Func<double, double> function,
        double firstGuess,
        double secondGuess,
        out RootFindResult result,
        out RootFindReport report,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateTolerancesAndIterations(
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        ValidateFiniteGuess(firstGuess, nameof(firstGuess));
        ValidateFiniteGuess(secondGuess, nameof(secondGuess));
        report = RunSecant(
            function,
            firstGuess,
            secondGuess,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        result = ToResult(report);
        return report.Succeeded;
    }

    /// <summary>Finds a root in a bracket using Brent's method.</summary>
    /// <remarks>
    /// Reference: Brent, <i>Algorithms for Minimization without Derivatives</i>,
    /// Prentice-Hall (1973), ISBN 0-13-022335-2.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The endpoints do not bracket a sign change.
    /// </exception>
    public static RootFindResult Brent(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        TryBrent(
            function,
            lower,
            upper,
            out var result,
            out var report,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        ThrowForInvalidBracket(report);
        return result;
    }

    /// <summary>
    /// Attempts Brent's method and reports bracket, callback, and iteration
    /// termination conditions.
    /// </summary>
    public static bool TryBrent(
        Func<double, double> function,
        double lower,
        double upper,
        out RootFindResult result,
        out RootFindReport report,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateBracket(
            lower,
            upper,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        report = RunBrent(
            function,
            lower,
            upper,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        result = ToResult(report);
        return report.Succeeded;
    }

    private static RootFindReport RunBisection(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        if (!TryEvaluate(function, lower, out var lowerValue) ||
            !TryEvaluate(function, upper, out var upperValue)) {
            return Failure(
                RootFindingMethod.Bisection,
                RootFindingStatus.FunctionEvaluationFailed,
                0);
        }

        if (lowerValue == 0) {
            return Success(
                RootFindingMethod.Bisection,
                lower,
                lowerValue,
                0);
        }

        if (upperValue == 0) {
            return Success(
                RootFindingMethod.Bisection,
                upper,
                upperValue,
                0);
        }

        if (Math.Sign(lowerValue) == Math.Sign(upperValue)) {
            return Failure(
                RootFindingMethod.Bisection,
                RootFindingStatus.BracketingFailure,
                0);
        }

        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            var middle = lower + ((upper - lower) * 0.5);
            if (!TryEvaluate(function, middle, out var middleValue)) {
                return Failure(
                    RootFindingMethod.Bisection,
                    RootFindingStatus.FunctionEvaluationFailed,
                    iteration);
            }

            if (middleValue == 0 ||
                IsConverged(lower, upper, absoluteTolerance, relativeTolerance)) {
                return Success(
                    RootFindingMethod.Bisection,
                    middle,
                    middleValue,
                    iteration);
            }

            if (Math.Sign(lowerValue) == Math.Sign(middleValue)) {
                lower = middle;
                lowerValue = middleValue;
            } else {
                upper = middle;
                upperValue = middleValue;
            }
        }

        return Failure(
            RootFindingMethod.Bisection,
            RootFindingStatus.MaximumIterationsReached,
            maximumIterations);
    }

    private static RootFindReport RunNewton(
        Func<double, double> function,
        Func<double, double> derivative,
        double initialGuess,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        var value = initialGuess;
        for (var iteration = 0; iteration <= maximumIterations; iteration++) {
            if (!TryEvaluate(function, value, out var functionValue)) {
                return Failure(
                    RootFindingMethod.Newton,
                    RootFindingStatus.FunctionEvaluationFailed,
                    iteration);
            }

            if (Math.Abs(functionValue) <= absoluteTolerance) {
                return Success(
                    RootFindingMethod.Newton,
                    value,
                    functionValue,
                    iteration);
            }

            if (iteration == maximumIterations) {
                break;
            }

            if (!TryEvaluate(derivative, value, out var derivativeValue)) {
                return Failure(
                    RootFindingMethod.Newton,
                    RootFindingStatus.DerivativeEvaluationFailed,
                    iteration);
            }

            if (derivativeValue == 0) {
                return Failure(
                    RootFindingMethod.Newton,
                    RootFindingStatus.ZeroDerivative,
                    iteration);
            }

            var step = functionValue / derivativeValue;
            var next = value - step;
            if (!double.IsFinite(next) || !double.IsFinite(step)) {
                return Failure(
                    RootFindingMethod.Newton,
                    RootFindingStatus.NonFiniteStep,
                    iteration + 1);
            }

            if (Math.Abs(step) <=
                absoluteTolerance + (relativeTolerance * Math.Abs(next))) {
                if (!TryEvaluate(function, next, out var residual)) {
                    return Failure(
                        RootFindingMethod.Newton,
                        RootFindingStatus.FunctionEvaluationFailed,
                        iteration + 1);
                }

                return Success(
                    RootFindingMethod.Newton,
                    next,
                    residual,
                    iteration + 1);
            }

            value = next;
        }

        return Failure(
            RootFindingMethod.Newton,
            RootFindingStatus.MaximumIterationsReached,
            maximumIterations);
    }

    private static RootFindReport RunSecant(
        Func<double, double> function,
        double firstGuess,
        double secondGuess,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        var previous = firstGuess;
        var current = secondGuess;
        if (!TryEvaluate(function, previous, out var previousValue) ||
            !TryEvaluate(function, current, out var currentValue)) {
            return Failure(
                RootFindingMethod.Secant,
                RootFindingStatus.FunctionEvaluationFailed,
                0);
        }

        if (currentValue == 0) {
            return Success(
                RootFindingMethod.Secant,
                current,
                currentValue,
                0);
        }

        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            var denominator = currentValue - previousValue;
            if (denominator == 0) {
                return Failure(
                    RootFindingMethod.Secant,
                    RootFindingStatus.ZeroDenominator,
                    iteration);
            }

            var step = currentValue * (current - previous) / denominator;
            var next = current - step;
            if (!double.IsFinite(next) || !double.IsFinite(step)) {
                return Failure(
                    RootFindingMethod.Secant,
                    RootFindingStatus.NonFiniteStep,
                    iteration);
            }

            if (!TryEvaluate(function, next, out var nextValue)) {
                return Failure(
                    RootFindingMethod.Secant,
                    RootFindingStatus.FunctionEvaluationFailed,
                    iteration);
            }

            if (Math.Abs(nextValue) <= absoluteTolerance ||
                Math.Abs(step) <=
                    absoluteTolerance + (relativeTolerance * Math.Abs(next))) {
                return Success(
                    RootFindingMethod.Secant,
                    next,
                    nextValue,
                    iteration);
            }

            previous = current;
            previousValue = currentValue;
            current = next;
            currentValue = nextValue;
        }

        return Failure(
            RootFindingMethod.Secant,
            RootFindingStatus.MaximumIterationsReached,
            maximumIterations);
    }

    private static RootFindReport RunBrent(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        var a = lower;
        var b = upper;
        if (!TryEvaluate(function, a, out var fa) ||
            !TryEvaluate(function, b, out var fb)) {
            return Failure(
                RootFindingMethod.Brent,
                RootFindingStatus.FunctionEvaluationFailed,
                0);
        }

        if (fa == 0) {
            return Success(RootFindingMethod.Brent, a, fa, 0);
        }

        if (fb == 0) {
            return Success(RootFindingMethod.Brent, b, fb, 0);
        }

        if (Math.Sign(fa) == Math.Sign(fb)) {
            return Failure(
                RootFindingMethod.Brent,
                RootFindingStatus.BracketingFailure,
                0);
        }

        var c = a;
        var fc = fa;
        var d = b - a;
        var e = d;
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            if (Math.Sign(fb) == Math.Sign(fc)) {
                c = a;
                fc = fa;
                d = b - a;
                e = d;
            }

            if (Math.Abs(fc) < Math.Abs(fb)) {
                (a, b, c) = (b, c, b);
                (fa, fb, fc) = (fb, fc, fb);
            }

            var tolerance = absoluteTolerance +
                (relativeTolerance * Math.Abs(b));
            var midpoint = 0.5 * (c - b);
            if (Math.Abs(midpoint) <= tolerance || fb == 0) {
                return Success(RootFindingMethod.Brent, b, fb, iteration);
            }

            if (Math.Abs(e) >= tolerance && Math.Abs(fa) > Math.Abs(fb)) {
                var s = fb / fa;
                double p;
                double q;
                if (a == c) {
                    p = 2 * midpoint * s;
                    q = 1 - s;
                } else {
                    var qa = fa / fc;
                    var r = fb / fc;
                    p = s * ((2 * midpoint * qa * (qa - r)) -
                        ((b - a) * (r - 1)));
                    q = (qa - 1) * (r - 1) * (s - 1);
                }

                if (p > 0) {
                    q = -q;
                } else {
                    p = -p;
                }

                if (2 * p < Math.Min(
                    (3 * midpoint * q) - Math.Abs(tolerance * q),
                    Math.Abs(e * q))) {
                    e = d;
                    d = p / q;
                } else {
                    d = midpoint;
                    e = midpoint;
                }
            } else {
                d = midpoint;
                e = midpoint;
            }

            a = b;
            fa = fb;
            b += Math.Abs(d) > tolerance
                ? d
                : Math.CopySign(tolerance, midpoint);
            if (!TryEvaluate(function, b, out fb)) {
                return Failure(
                    RootFindingMethod.Brent,
                    RootFindingStatus.FunctionEvaluationFailed,
                    iteration);
            }
        }

        return Failure(
            RootFindingMethod.Brent,
            RootFindingStatus.MaximumIterationsReached,
            maximumIterations);
    }

    private static bool TryEvaluate(
        Func<double, double> function,
        double value,
        out double result) {
        result = function(value);
        return double.IsFinite(result);
    }

    private static bool IsConverged(
        double lower,
        double upper,
        double absoluteTolerance,
        double relativeTolerance) {
        var middle = 0.5 * (lower + upper);
        return Math.Abs(upper - lower) <=
            absoluteTolerance + (relativeTolerance * Math.Abs(middle));
    }

    private static RootFindReport Success(
        RootFindingMethod method,
        double root,
        double residual,
        int iterations) {
        return new RootFindReport(
            method,
            RootFindingStatus.Succeeded,
            root,
            residual,
            iterations);
    }

    private static RootFindReport Failure(
        RootFindingMethod method,
        RootFindingStatus status,
        int iterations) {
        return new RootFindReport(
            method,
            status,
            double.NaN,
            double.NaN,
            iterations);
    }

    private static RootFindResult ToResult(RootFindReport report) {
        return new RootFindResult(
            report.Root,
            report.Residual,
            report.Iterations,
            report.Succeeded);
    }

    private static void ThrowForInvalidBracket(RootFindReport report) {
        if (report.Status == RootFindingStatus.BracketingFailure) {
            throw new ArgumentException(
                "The interval endpoints must bracket a sign change.");
        }
    }

    private static void ValidateBracket(
        double lower,
        double upper,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        ValidateTolerancesAndIterations(
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        ValidateFiniteGuess(lower, nameof(lower));
        ValidateFiniteGuess(upper, nameof(upper));
        if (lower >= upper) {
            throw new ArgumentException(
                "The lower endpoint must be less than the upper endpoint.",
                nameof(upper));
        }
    }

    private static void ValidateTolerancesAndIterations(
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        if (!double.IsFinite(absoluteTolerance) || absoluteTolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(absoluteTolerance));
        }

        if (!double.IsFinite(relativeTolerance) || relativeTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIterations);
    }

    private static void ValidateFiniteGuess(double value, string parameterName) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
