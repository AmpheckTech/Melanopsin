// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers;

namespace Melanopsin.Mathematics;

/// <summary>
/// Deterministic one-dimensional numerical quadrature algorithms.
/// </summary>
/// <remarks>
/// Bounds are ordered automatically: reversing them changes the sign of the
/// result. Integrands must return finite values at every requested point, and
/// adaptive rules must meet their requested tolerance. The scalar return-value
/// APIs throw <see cref="ArithmeticException"/> when an integrand is non-finite,
/// an estimate becomes non-finite, or a refinement budget is exhausted. The
/// result-returning
/// <see cref="NumericalIntegration.Integrate(IntegrationRule, Func{double, double}, double, double, double, int)"/>
/// API reports the same conditions through <see cref="QuadratureStatus"/> and
/// retains the best finite estimate when a refinement limit is reached.
/// The embedded Gauss-Kronrod construction follows the conventions in Piessens
/// et al., <i>QUADPACK: A Subroutine Package for Automatic Integration</i>
/// (1983).
/// </remarks>
public static partial class NumericalIntegration {
    private const int DefaultIntervals = 1000;
    private const int DefaultMaximumDepth = 20;
    private const int DefaultMaximumLevels = 12;
    private const double DefaultTolerance = 1e-10;

    public static double Trapezoidal(
        Func<double, double> function,
        double lower,
        double upper,
        int intervalCount = DefaultIntervals) {
        var result = TryTrapezoidal(
            function,
            lower,
            upper,
            intervalCount,
            out var status);
        ThrowForNumericalFailure(status);
        return result;
    }

    private static double TryTrapezoidal(
        Func<double, double> function,
        double lower,
        double upper,
        int intervalCount,
        out QuadratureStatus status) {
        Validate(function, intervalCount);
        if (lower == upper) {
            status = QuadratureStatus.Succeeded;
            return 0;
        }

        if (lower > upper) {
            var reversed = TryTrapezoidal(
                function,
                upper,
                lower,
                intervalCount,
                out status);
            return double.IsFinite(reversed) ? -reversed : double.NaN;
        }

        ValidateBounds(lower, upper);
        var width = upper - lower;
        var step = width / intervalCount;
        if (!TryEvaluate(function, lower, out var lowerValue) ||
            !TryEvaluate(function, upper, out var upperValue)) {
            status = QuadratureStatus.NonFiniteFunctionValue;
            return double.NaN;
        }

        var sum = 0.5 * (lowerValue + upperValue);
        for (var index = 1; index < intervalCount; index++) {
            if (!TryEvaluate(function, lower + (step * index), out var value)) {
                status = QuadratureStatus.NonFiniteFunctionValue;
                return double.NaN;
            }

            sum += value;
        }

        var result = sum * step;
        if (!double.IsFinite(result)) {
            status = QuadratureStatus.NonFiniteEstimate;
            return double.NaN;
        }

        status = QuadratureStatus.Succeeded;
        return result;
    }

    public static double Simpson(
        Func<double, double> function,
        double lower,
        double upper,
        int intervalCount = DefaultIntervals) {
        var result = TrySimpson(
            function,
            lower,
            upper,
            intervalCount,
            out var status);
        ThrowForNumericalFailure(status);
        return result;
    }

    private static double TrySimpson(
        Func<double, double> function,
        double lower,
        double upper,
        int intervalCount,
        out QuadratureStatus status) {
        Validate(function, intervalCount);
        if ((intervalCount & 1) != 0) {
            throw new ArgumentOutOfRangeException(
                nameof(intervalCount),
                intervalCount,
                "Composite Simpson integration requires an even interval count.");
        }

        if (lower == upper) {
            status = QuadratureStatus.Succeeded;
            return 0;
        }

        if (lower > upper) {
            var reversed = TrySimpson(
                function,
                upper,
                lower,
                intervalCount,
                out status);
            return double.IsFinite(reversed) ? -reversed : double.NaN;
        }

        ValidateBounds(lower, upper);
        var step = (upper - lower) / intervalCount;
        if (!TryEvaluate(function, lower, out var lowerValue) ||
            !TryEvaluate(function, upper, out var upperValue)) {
            status = QuadratureStatus.NonFiniteFunctionValue;
            return double.NaN;
        }

        var sum = lowerValue + upperValue;
        for (var index = 1; index < intervalCount; index++) {
            if (!TryEvaluate(function, lower + (step * index), out var value)) {
                status = QuadratureStatus.NonFiniteFunctionValue;
                return double.NaN;
            }

            var weight = (index & 1) == 0 ? 2.0 : 4.0;
            sum += weight * value;
        }

        var result = sum * step / 3;
        if (!double.IsFinite(result)) {
            status = QuadratureStatus.NonFiniteEstimate;
            return double.NaN;
        }

        status = QuadratureStatus.Succeeded;
        return result;
    }

    /// <summary>
    /// Integrates with adaptive Simpson refinement.
    /// </summary>
    public static double AdaptiveSimpson(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance = DefaultTolerance,
        int maximumDepth = DefaultMaximumDepth) {
        var result = IntegrateAdaptiveSimpson(
            function,
            lower,
            upper,
            absoluteTolerance,
            maximumDepth,
            out var status);
        ThrowForNumericalFailure(status);
        return result;
    }

    private static double IntegrateAdaptiveSimpson(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        int maximumDepth,
        out QuadratureStatus status) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateTolerance(absoluteTolerance);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);
        if (lower == upper) {
            status = QuadratureStatus.Succeeded;
            return 0;
        }

        if (lower > upper) {
            return -IntegrateAdaptiveSimpson(
                function,
                upper,
                lower,
                absoluteTolerance,
                maximumDepth,
                out status);
        }

        ValidateBounds(lower, upper);
        var middle = 0.5 * (lower + upper);
        if (!TryEvaluate(function, lower, out var leftValue) ||
            !TryEvaluate(function, middle, out var middleValue) ||
            !TryEvaluate(function, upper, out var rightValue)) {
            status = QuadratureStatus.NonFiniteFunctionValue;
            return double.NaN;
        }

        var whole = SimpsonEstimate(lower, upper, leftValue, middleValue, rightValue);
        if (!double.IsFinite(whole)) {
            status = QuadratureStatus.NonFiniteEstimate;
            return double.NaN;
        }

        if (!TryRefine(
            function,
            lower,
            upper,
            leftValue,
            middleValue,
            rightValue,
            whole,
            absoluteTolerance,
            0,
            maximumDepth,
            out var result,
            out var toleranceSatisfied,
            out status)) {
            return double.NaN;
        }

        status = toleranceSatisfied
            ? QuadratureStatus.Succeeded
            : QuadratureStatus.MaximumDepthReached;
        return result;
    }

    /// <summary>
    /// Integrates using a five-point Gauss-Legendre rule.
    /// </summary>
    /// <remarks>
    /// The nodes and weights follow the Gauss quadrature conventions in
    /// <see href="https://dlmf.nist.gov/3.5">NIST DLMF §3.5</see>.
    /// </remarks>
    public static double GaussLegendre5(
        Func<double, double> function,
        double lower,
        double upper) {
        var result = TryGaussLegendre5(
            function,
            lower,
            upper,
            out var status);
        ThrowForNumericalFailure(status);
        return result;
    }

    private static double TryGaussLegendre5(
        Func<double, double> function,
        double lower,
        double upper,
        out QuadratureStatus status) {
        ArgumentNullException.ThrowIfNull(function);
        if (lower == upper) {
            status = QuadratureStatus.Succeeded;
            return 0;
        }

        if (lower > upper) {
            var reversed = TryGaussLegendre5(
                function,
                upper,
                lower,
                out status);
            return double.IsFinite(reversed) ? -reversed : double.NaN;
        }

        ValidateBounds(lower, upper);
        var halfWidth = 0.5 * (upper - lower);
        var midpoint = 0.5 * (upper + lower);
        if (!TryEvaluate(function, midpoint, out var center) ||
            !TryEvaluate(
                function,
                midpoint + (0.5384693101056831 * halfWidth),
                out var innerRight) ||
            !TryEvaluate(
                function,
                midpoint - (0.5384693101056831 * halfWidth),
                out var innerLeft) ||
            !TryEvaluate(
                function,
                midpoint + (0.9061798459386640 * halfWidth),
                out var outerRight) ||
            !TryEvaluate(
                function,
                midpoint - (0.9061798459386640 * halfWidth),
                out var outerLeft)) {
            status = QuadratureStatus.NonFiniteFunctionValue;
            return double.NaN;
        }

        var result =
            (0.5688888888888889 * center) +
            (0.4786286704993665 *
                (innerRight + innerLeft)) +
            (0.2369268850561891 *
                (outerRight + outerLeft));
        var estimate = halfWidth * result;
        if (!double.IsFinite(estimate)) {
            status = QuadratureStatus.NonFiniteEstimate;
            return double.NaN;
        }

        status = QuadratureStatus.Succeeded;
        return estimate;
    }

    /// <summary>
    /// Integrates with the fixed 15-point Gauss-Kronrod rule and returns its
    /// embedded 7-point Gauss error estimate.
    /// </summary>
    /// <param name="function">The finite-valued integrand.</param>
    /// <param name="lower">The lower integration bound.</param>
    /// <param name="upper">The upper integration bound.</param>
    /// <param name="errorEstimate">
    /// Receives the absolute difference between the 15-point Kronrod and
    /// embedded 7-point Gauss estimates. This is an estimate, not a guaranteed
    /// rigorous error bound.
    /// </param>
    public static double GaussKronrod15(
        Func<double, double> function,
        double lower,
        double upper,
        out double errorEstimate) {
        var result = TryGaussKronrod15(
            function,
            lower,
            upper,
            out errorEstimate,
            out var status);
        ThrowForNumericalFailure(status);
        return result;
    }

    private static double TryGaussKronrod15(
        Func<double, double> function,
        double lower,
        double upper,
        out double errorEstimate,
        out QuadratureStatus status) {
        ArgumentNullException.ThrowIfNull(function);
        if (lower == upper) {
            errorEstimate = 0;
            status = QuadratureStatus.Succeeded;
            return 0;
        }

        if (lower > upper) {
            var reversed = TryGaussKronrod15(
                function,
                upper,
                lower,
                out errorEstimate,
                out status);
            return double.IsFinite(reversed) ? -reversed : double.NaN;
        }

        ValidateBounds(lower, upper);
        if (!TryEvaluateGaussKronrod(
            function,
            lower,
            upper,
            out var integral,
            out errorEstimate,
            out status)) {
            errorEstimate = double.NaN;
            return double.NaN;
        }

        status = QuadratureStatus.Succeeded;
        return integral;
    }

    /// <summary>
    /// Integrates adaptively by bisecting intervals until the embedded
    /// Gauss-Kronrod error estimate satisfies a local absolute-or-relative
    /// tolerance.
    /// </summary>
    /// <remarks>
    /// The error estimator is not a rigorous enclosure. When the maximum depth
    /// is reached, the current Kronrod estimate is accepted rather than
    /// discarding the partial result. Integrands must remain finite.
    /// </remarks>
    public static double AdaptiveGaussKronrod(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance = DefaultTolerance,
        double relativeTolerance = DefaultTolerance,
        int maximumDepth = 30) {
        var result = IntegrateAdaptiveGaussKronrod(
            function,
            lower,
            upper,
            absoluteTolerance,
            relativeTolerance,
            maximumDepth,
            out var status);
        ThrowForNumericalFailure(status);
        return result;
    }

    private static double IntegrateAdaptiveGaussKronrod(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumDepth,
        out QuadratureStatus status) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateTolerance(absoluteTolerance);
        ValidateTolerance(relativeTolerance);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);
        if (lower == upper) {
            status = QuadratureStatus.Succeeded;
            return 0;
        }

        if (lower > upper) {
            return -IntegrateAdaptiveGaussKronrod(
                function,
                upper,
                lower,
                absoluteTolerance,
                relativeTolerance,
                maximumDepth,
                out status);
        }

        ValidateBounds(lower, upper);
        if (!TryEvaluateGaussKronrod(
            function,
            lower,
            upper,
            out var integral,
            out var error,
            out status)) {
            return double.NaN;
        }

        var tolerance = Math.Max(
            absoluteTolerance,
            relativeTolerance * Math.Abs(integral));
        if (!TryRefineGaussKronrod(
            function,
            lower,
            upper,
            integral,
            error,
            tolerance,
            0,
            maximumDepth,
            out var result,
            out var toleranceSatisfied,
            out status)) {
            return double.NaN;
        }

        status = toleranceSatisfied
            ? QuadratureStatus.Succeeded
            : QuadratureStatus.MaximumDepthReached;
        return result;
    }

    /// <summary>
    /// The minimum supported Romberg refinement level count.
    /// </summary>
    public const int MinimumRombergLevels = 2;

    /// <summary>
    /// The maximum supported Romberg refinement level count.
    /// </summary>
    /// <remarks>
    /// The limit keeps binary interval splitting representable by the
    /// implementation's <see cref="long"/> indexing arithmetic. It is not a
    /// numerical near-singularity threshold.
    /// </remarks>
    public const int MaximumRombergLevels = 63;

    /// <summary>
    /// Gets the caller-workspace length required by <see cref="TryRomberg"/>.
    /// </summary>
    /// <param name="maximumLevels">The requested Romberg refinement levels.</param>
    /// <remarks>
    /// Romberg's extrapolation table is stored in packed triangular form, so
    /// the requirement is <c>maximumLevels * (maximumLevels + 1) / 2</c>
    /// doubles rather than a full square table.
    /// </remarks>
    public static int GetRombergWorkspaceLength(int maximumLevels) {
        ValidateRombergLevels(maximumLevels);
        return (int)(((long)maximumLevels * (maximumLevels + 1)) / 2);
    }

    /// <summary>
    /// Integrates using Romberg extrapolation of the composite trapezoidal
    /// rule.
    /// </summary>
    /// <remarks>
    /// The first and second rows of the extrapolation table are always
    /// evaluated, even when the tolerance is already satisfied. Non-finite
    /// samples or estimates throw <see cref="ArithmeticException"/>. Exhausting
    /// the requested levels while producing finite estimates also throws
    /// <see cref="ArithmeticException"/>.
    /// </remarks>
    public static double Romberg(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance = DefaultTolerance,
        int maximumLevels = DefaultMaximumLevels) {
        ValidateRombergArguments(
            function,
            lower,
            upper,
            absoluteTolerance,
            maximumLevels);
        _ = TryRombergPooled(
            function,
            lower,
            upper,
            absoluteTolerance,
            maximumLevels,
            out var estimate,
            out var status);
        ThrowForNumericalFailure(status);
        return estimate;
    }

    /// <summary>
    /// Attempts Romberg integration using caller-owned workspace and returns
    /// numerical failure as a status instead of throwing.
    /// </summary>
    /// <param name="function">The finite-valued integrand.</param>
    /// <param name="lower">The first integration bound.</param>
    /// <param name="upper">The second integration bound.</param>
    /// <param name="absoluteTolerance">The absolute tolerance for the final extrapolation difference.</param>
    /// <param name="maximumLevels">The maximum refinement levels, from 2 through 63.</param>
    /// <param name="workspace">
    /// Caller-owned storage with at least
    /// <see cref="GetRombergWorkspaceLength"/> elements. The contents need not
    /// be initialized and are unspecified after the call.
    /// </param>
    /// <param name="estimate">
    /// Receives the best finite estimate, or NaN when the integrand or an
    /// intermediate is non-finite.
    /// </param>
    /// <param name="status">The termination status.</param>
    /// <returns>
    /// <see langword="true"/> when finite estimates were produced, even if the
    /// tolerance was not met; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Invalid arguments and insufficient workspace are programming errors and
    /// throw argument exceptions. Reversing the bounds changes the sign of the
    /// estimate. A finite estimate with
    /// <see cref="QuadratureStatus.MaximumLevelsReached"/> is usable but does
    /// not satisfy the requested tolerance.
    /// </remarks>
    public static bool TryRomberg(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        int maximumLevels,
        Span<double> workspace,
        out double estimate,
        out QuadratureStatus status) {
        ValidateRombergArguments(
            function,
            lower,
            upper,
            absoluteTolerance,
            maximumLevels);
        var requiredLength = GetRombergWorkspaceLength(maximumLevels);
        if (workspace.Length < requiredLength) {
            throw new ArgumentException(
                $"Romberg integration requires at least {requiredLength} workspace values.",
                nameof(workspace));
        }

        return TryIntegrateRomberg(
            function,
            lower,
            upper,
            absoluteTolerance,
            maximumLevels,
            workspace[..requiredLength],
            out estimate,
            out status);
    }

    private static bool TryRombergPooled(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        int maximumLevels,
        out double estimate,
        out QuadratureStatus status) {
        var requiredLength = GetRombergWorkspaceLength(maximumLevels);
        var workspace = ArrayPool<double>.Shared.Rent(requiredLength);
        try {
            return TryIntegrateRomberg(
                function,
                lower,
                upper,
                absoluteTolerance,
                maximumLevels,
                workspace.AsSpan(0, requiredLength),
                out estimate,
                out status);
        } finally {
            ArrayPool<double>.Shared.Return(workspace);
        }
    }

    private static bool TryIntegrateRomberg(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        int maximumLevels,
        Span<double> table,
        out double estimate,
        out QuadratureStatus status) {
        if (lower == upper) {
            estimate = 0;
            status = QuadratureStatus.Succeeded;
            return true;
        }

        if (lower > upper) {
            if (!TryIntegrateRomberg(
                function,
                upper,
                lower,
                absoluteTolerance,
                maximumLevels,
                table,
                out estimate,
                out status)) {
                return false;
            }

            estimate = -estimate;
            return true;
        }

        var width = upper - lower;
        if (!TryEvaluate(function, lower, out var lowerValue) ||
            !TryEvaluate(function, upper, out var upperValue)) {
            estimate = double.NaN;
            status = QuadratureStatus.NonFiniteFunctionValue;
            return false;
        }

        table[0] = 0.5 * width * (lowerValue + upperValue);
        if (!double.IsFinite(table[0])) {
            estimate = double.NaN;
            status = QuadratureStatus.NonFiniteEstimate;
            return false;
        }

        status = QuadratureStatus.MaximumLevelsReached;
        for (var level = 1; level < maximumLevels; level++) {
            var currentBase = (level * (level + 1)) / 2;
            var previousBase = ((level - 1) * level) / 2;
            var step = width / (1L << level);
            var sum = 0.0;
            var pointCount = 1L << (level - 1);
            for (var index = 1L; index <= pointCount; index++) {
                if (!TryEvaluate(
                        function,
                        lower + (((2 * index) - 1) * step),
                        out var value)) {
                    estimate = double.NaN;
                    status = QuadratureStatus.NonFiniteFunctionValue;
                    return false;
                }

                sum += value;
            }

            table[currentBase] =
                (0.5 * table[previousBase]) + (step * sum);
            if (!double.IsFinite(table[currentBase])) {
                estimate = double.NaN;
                status = QuadratureStatus.NonFiniteEstimate;
                return false;
            }

            var factor = 1.0;
            for (var order = 1; order <= level; order++) {
                factor *= 4;
                table[currentBase + order] =
                    table[currentBase + order - 1] +
                    ((table[currentBase + order - 1] -
                        table[previousBase + order - 1]) /
                        (factor - 1));
                if (!double.IsFinite(table[currentBase + order])) {
                    estimate = double.NaN;
                    status = QuadratureStatus.NonFiniteEstimate;
                    return false;
                }
            }

            if (level > 1 &&
                Math.Abs(
                    table[currentBase + level] -
                    table[previousBase + level - 1]) <=
                absoluteTolerance) {
                estimate = table[currentBase + level];
                status = QuadratureStatus.Succeeded;
                return true;
            }
        }

        var finalBase = ((maximumLevels - 1) * maximumLevels) / 2;
        estimate = table[finalBase + maximumLevels - 1];
        status = QuadratureStatus.MaximumLevelsReached;
        return true;
    }

    private static void ValidateRombergArguments(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        int maximumLevels) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateBounds(lower, upper);
        ValidateTolerance(absoluteTolerance);
        ValidateRombergLevels(maximumLevels);
    }

    private static void ValidateRombergLevels(int maximumLevels) {
        if (maximumLevels < MinimumRombergLevels ||
            maximumLevels > MaximumRombergLevels) {
            throw new ArgumentOutOfRangeException(
                nameof(maximumLevels),
                maximumLevels,
                $"Romberg integration requires between {MinimumRombergLevels} and {MaximumRombergLevels} levels.");
        }
    }

    private static bool TryRefine(
        Func<double, double> function,
        double lower,
        double upper,
        double leftValue,
        double middleValue,
        double rightValue,
        double whole,
        double tolerance,
        int depth,
        int maximumDepth,
        out double result,
        out bool toleranceSatisfied,
        out QuadratureStatus status) {
        var middle = 0.5 * (lower + upper);
        var leftMiddle = 0.5 * (lower + middle);
        var rightMiddle = 0.5 * (middle + upper);
        if (!TryEvaluate(function, leftMiddle, out var leftMiddleValue) ||
            !TryEvaluate(function, rightMiddle, out var rightMiddleValue)) {
            result = double.NaN;
            toleranceSatisfied = false;
            status = QuadratureStatus.NonFiniteFunctionValue;
            return false;
        }

        var left = SimpsonEstimate(
            lower,
            middle,
            leftValue,
            leftMiddleValue,
            middleValue);
        var right = SimpsonEstimate(
            middle,
            upper,
            middleValue,
            rightMiddleValue,
            rightValue);
        var refined = left + right;
        var error = refined - whole;
        if (!double.IsFinite(refined) || !double.IsFinite(error)) {
            result = double.NaN;
            toleranceSatisfied = false;
            status = QuadratureStatus.NonFiniteEstimate;
            return false;
        }

        if (Math.Abs(error) <= 15 * tolerance) {
            result = refined + (error / 15);
            if (!double.IsFinite(result)) {
                result = double.NaN;
                toleranceSatisfied = false;
                status = QuadratureStatus.NonFiniteEstimate;
                return false;
            }

            toleranceSatisfied = true;
            status = QuadratureStatus.Succeeded;
            return true;
        }

        if (depth >= maximumDepth) {
            result = refined + (error / 15);
            toleranceSatisfied = false;
            if (!double.IsFinite(result)) {
                result = double.NaN;
                status = QuadratureStatus.NonFiniteEstimate;
                return false;
            }

            status = QuadratureStatus.MaximumDepthReached;
            return true;
        }

        if (!TryRefine(
            function,
            lower,
            middle,
            leftValue,
            leftMiddleValue,
            middleValue,
            left,
            tolerance * 0.5,
            depth + 1,
            maximumDepth,
            out var leftEstimate,
            out var leftConverged,
            out status)) {
            result = double.NaN;
            toleranceSatisfied = false;
            return false;
        }

        if (!TryRefine(
            function,
            middle,
            upper,
            middleValue,
            rightMiddleValue,
            rightValue,
            right,
            tolerance * 0.5,
            depth + 1,
            maximumDepth,
            out var rightEstimate,
            out var rightConverged,
            out status)) {
            result = double.NaN;
            toleranceSatisfied = false;
            return false;
        }

        toleranceSatisfied = leftConverged && rightConverged;
        result = leftEstimate + rightEstimate;
        if (!double.IsFinite(result)) {
            result = double.NaN;
            toleranceSatisfied = false;
            status = QuadratureStatus.NonFiniteEstimate;
            return false;
        }

        status = toleranceSatisfied
            ? QuadratureStatus.Succeeded
            : QuadratureStatus.MaximumDepthReached;
        return true;
    }

    private static bool TryRefineGaussKronrod(
        Func<double, double> function,
        double lower,
        double upper,
        double integral,
        double error,
        double tolerance,
        int depth,
        int maximumDepth,
        out double result,
        out bool toleranceSatisfied,
        out QuadratureStatus status) {
        if (error <= tolerance) {
            result = integral;
            toleranceSatisfied = true;
            status = QuadratureStatus.Succeeded;
            return true;
        }

        if (depth >= maximumDepth) {
            result = integral;
            toleranceSatisfied = false;
            status = QuadratureStatus.MaximumDepthReached;
            return true;
        }

        var middle = 0.5 * (lower + upper);
        if (!TryEvaluateGaussKronrod(
            function,
            lower,
            middle,
            out var leftIntegral,
            out var leftError,
            out status)) {
            result = double.NaN;
            toleranceSatisfied = false;
            return false;
        }

        if (!TryEvaluateGaussKronrod(
            function,
            middle,
            upper,
            out var rightIntegral,
            out var rightError,
            out status)) {
            result = double.NaN;
            toleranceSatisfied = false;
            return false;
        }

        var halfTolerance = tolerance * 0.5;
        if (!TryRefineGaussKronrod(
            function,
            lower,
            middle,
            leftIntegral,
            leftError,
            halfTolerance,
            depth + 1,
            maximumDepth,
            out var leftEstimate,
            out var leftConverged,
            out status)) {
            result = double.NaN;
            toleranceSatisfied = false;
            return false;
        }

        if (!TryRefineGaussKronrod(
            function,
            middle,
            upper,
            rightIntegral,
            rightError,
            halfTolerance,
            depth + 1,
            maximumDepth,
            out var rightEstimate,
            out var rightConverged,
            out status)) {
            result = double.NaN;
            toleranceSatisfied = false;
            return false;
        }

        toleranceSatisfied = leftConverged && rightConverged;
        result = leftEstimate + rightEstimate;
        if (!double.IsFinite(result)) {
            result = double.NaN;
            toleranceSatisfied = false;
            status = QuadratureStatus.NonFiniteEstimate;
            return false;
        }

        status = toleranceSatisfied
            ? QuadratureStatus.Succeeded
            : QuadratureStatus.MaximumDepthReached;
        return true;
    }

    private static bool TryEvaluateGaussKronrod(
        Func<double, double> function,
        double lower,
        double upper,
        out double integral,
        out double errorEstimate,
        out QuadratureStatus status) {
        ReadOnlySpan<double> nodes = [
            0.9914553711208126,
            0.9491079123427585,
            0.8648644233597691,
            0.7415311855993945,
            0.5860872354676911,
            0.4058451513773972,
            0.2077849550078985,
        ];
        ReadOnlySpan<double> kronrodWeights = [
            0.02293532201052922,
            0.06309209262997855,
            0.1047900103222502,
            0.1406532597155259,
            0.1690047266392679,
            0.1903505780647854,
            0.2044329400752989,
        ];
        ReadOnlySpan<double> gaussWeights = [
            0,
            0.1294849661688697,
            0,
            0.2797053914892767,
            0,
            0.3818300505051189,
            0,
        ];

        var midpoint = 0.5 * (lower + upper);
        var halfWidth = 0.5 * (upper - lower);
        var kronrodSum = 0.0;
        var gaussSum = 0.0;
        for (var index = 0; index < nodes.Length; index++) {
            var offset = halfWidth * nodes[index];
            if (!TryEvaluate(function, midpoint - offset, out var left) ||
                !TryEvaluate(function, midpoint + offset, out var right)) {
                integral = double.NaN;
                errorEstimate = double.NaN;
                status = QuadratureStatus.NonFiniteFunctionValue;
                return false;
            }

            var pair = left + right;
            kronrodSum += kronrodWeights[index] * pair;
            gaussSum += gaussWeights[index] * pair;
        }

        if (!TryEvaluate(function, midpoint, out var center)) {
            integral = double.NaN;
            errorEstimate = double.NaN;
            status = QuadratureStatus.NonFiniteFunctionValue;
            return false;
        }

        kronrodSum += 0.2094821410847278 * center;
        gaussSum += 0.4179591836734694 * center;
        integral = halfWidth * kronrodSum;
        var gauss = halfWidth * gaussSum;
        errorEstimate = Math.Abs(integral - gauss);
        if (!double.IsFinite(integral) || !double.IsFinite(errorEstimate)) {
            integral = double.NaN;
            errorEstimate = double.NaN;
            status = QuadratureStatus.NonFiniteEstimate;
            return false;
        }

        status = QuadratureStatus.Succeeded;
        return true;
    }

    private static double SimpsonEstimate(
        double lower,
        double upper,
        double leftValue,
        double middleValue,
        double rightValue) {
        return (upper - lower) *
            (leftValue + (4 * middleValue) + rightValue) / 6;
    }

    private static void Validate(
        Func<double, double> function,
        int intervalCount) {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalCount);
    }

    private static void ValidateBounds(double lower, double upper) {
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) {
            throw new ArgumentOutOfRangeException(nameof(lower));
        }
    }

    private static void ValidateTolerance(double tolerance) {
        if (!double.IsFinite(tolerance) || tolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
    }

    private static bool TryEvaluate(
        Func<double, double> function,
        double value,
        out double result) {
        result = function(value);
        return double.IsFinite(result);
    }

    private static void ThrowForNumericalFailure(QuadratureStatus status) {
        switch (status) {
            case QuadratureStatus.NonFiniteFunctionValue:
                throw new ArithmeticException(
                    "The integrand returned a non-finite value.");
            case QuadratureStatus.NonFiniteEstimate:
                throw new ArithmeticException(
                    "The numerical integration result is non-finite.");
            case QuadratureStatus.MaximumDepthReached:
            case QuadratureStatus.MaximumLevelsReached:
                throw new ArithmeticException(
                    "The numerical integration tolerance was not met within the refinement limit.");
            default:
                break;
        }
    }
}
