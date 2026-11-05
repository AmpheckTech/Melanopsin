// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// The outcome of a one-dimensional minimization.
/// </summary>
public readonly record struct OptimizationResult {
    public OptimizationResult(
        double point,
        double value,
        int iterations,
        bool converged) {
        Point = point;
        Value = value;
        Iterations = iterations;
        Converged = converged;
    }

    public double Point { get; }

    public double Value { get; }

    public int Iterations { get; }

    public bool Converged { get; }

    internal static OptimizationResult Failed(int iterations) {
        return new OptimizationResult(
            double.NaN,
            double.NaN,
            iterations,
            false);
    }
}

/// <summary>
/// Deterministic scalar and gradient-based minimization algorithms.
/// </summary>
public static partial class Optimization {
    private const double GoldenRatio = 0.6180339887498948482045868343656381;
    private const double DefaultAbsoluteTolerance = 1e-10;
    private const double DefaultRelativeTolerance = 1e-10;
    private const int DefaultMaximumIterations = 100;
    private const int MaximumBacktrackingSteps = 40;

    /// <summary>
    /// Minimizes a unimodal function with golden-section search.
    /// </summary>
    /// <remarks>
    /// Invalid arguments throw. A non-finite objective value throws
    /// <see cref="ArithmeticException"/> so that the legacy convenience API
    /// never silently invents a minimum. Use
    /// <see cref="TryGoldenSection(Func{double, double}, double, double, out OptimizationResult, out OptimizationReport, double, double, int)"/>
    /// when numerical failure must be inspected without an exception.
    /// </remarks>
    public static OptimizationResult GoldenSection(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        TryGoldenSection(
            function,
            lower,
            upper,
            out var result,
            out var report,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        ThrowIfFunctionEvaluationFailed(report);
        return result;
    }

    /// <summary>
    /// Attempts a golden-section minimization and reports the numerical
    /// termination reason.
    /// </summary>
    public static bool TryGoldenSection(
        Func<double, double> function,
        double lower,
        double upper,
        out OptimizationResult result,
        out OptimizationReport report,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        ValidateBracket(
            function,
            lower,
            upper,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        var left = lower;
        var right = upper;
        var first = right - (GoldenRatio * (right - left));
        var second = left + (GoldenRatio * (right - left));
        var functionEvaluations = 0;
        if (!TryEvaluate(
                function,
                first,
                ref functionEvaluations,
                out var firstValue) ||
            !TryEvaluate(
                function,
                second,
                ref functionEvaluations,
                out var secondValue)) {
            result = OptimizationResult.Failed(0);
            report = CreateScalarReport(
                OptimizationMethod.GoldenSection,
                OptimizationStatus.FunctionEvaluationFailed,
                0,
                functionEvaluations,
                double.NaN,
                double.NaN);
            return false;
        }

        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            var width = Math.Abs(right - left);
            if (width <=
                absoluteTolerance +
                    (relativeTolerance * Math.Max(Math.Abs(left), Math.Abs(right)))) {
                var point = 0.5 * (left + right);
                if (!TryEvaluate(
                        function,
                        point,
                        ref functionEvaluations,
                        out var value)) {
                    result = OptimizationResult.Failed(iteration);
                    report = CreateScalarReport(
                        OptimizationMethod.GoldenSection,
                        OptimizationStatus.FunctionEvaluationFailed,
                        iteration,
                        functionEvaluations,
                        double.NaN,
                        width);
                    return false;
                }

                result = new OptimizationResult(point, value, iteration, true);
                report = CreateScalarReport(
                    OptimizationMethod.GoldenSection,
                    OptimizationStatus.Succeeded,
                    iteration,
                    functionEvaluations,
                    value,
                    width);
                return true;
            }

            if (firstValue <= secondValue) {
                right = second;
                second = first;
                secondValue = firstValue;
                first = right - (GoldenRatio * (right - left));
                if (!TryEvaluate(
                        function,
                        first,
                        ref functionEvaluations,
                        out firstValue)) {
                    result = OptimizationResult.Failed(iteration);
                    report = CreateScalarReport(
                        OptimizationMethod.GoldenSection,
                        OptimizationStatus.FunctionEvaluationFailed,
                        iteration,
                        functionEvaluations,
                        double.NaN,
                        width);
                    return false;
                }
            } else {
                left = first;
                first = second;
                firstValue = secondValue;
                second = left + (GoldenRatio * (right - left));
                if (!TryEvaluate(
                        function,
                        second,
                        ref functionEvaluations,
                        out secondValue)) {
                    result = OptimizationResult.Failed(iteration);
                    report = CreateScalarReport(
                        OptimizationMethod.GoldenSection,
                        OptimizationStatus.FunctionEvaluationFailed,
                        iteration,
                        functionEvaluations,
                        double.NaN,
                        width);
                    return false;
                }
            }
        }

        result = OptimizationResult.Failed(maximumIterations);
        report = CreateScalarReport(
            OptimizationMethod.GoldenSection,
            OptimizationStatus.MaximumIterationsReached,
            maximumIterations,
            functionEvaluations,
            Math.Min(firstValue, secondValue),
            Math.Abs(right - left));
        return false;
    }

    /// <summary>
    /// Minimizes a unimodal function in a bracket using Brent's method.
    /// </summary>
    /// <remarks>
    /// Invalid arguments throw. A non-finite objective value throws
    /// <see cref="ArithmeticException"/>. Use <see cref="TryBrent"/> when
    /// numerical failure must be inspected without an exception.
    /// </remarks>
    public static OptimizationResult Brent(
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
        ThrowIfFunctionEvaluationFailed(report);
        return result;
    }

    /// <summary>
    /// Attempts a Brent minimization and reports the numerical termination
    /// reason.
    /// </summary>
    public static bool TryBrent(
        Func<double, double> function,
        double lower,
        double upper,
        out OptimizationResult result,
        out OptimizationReport report,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance,
        int maximumIterations = DefaultMaximumIterations) {
        ValidateBracket(
            function,
            lower,
            upper,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        var a = lower;
        var b = upper;
        var x = a + (GoldenRatio * (b - a));
        var w = x;
        var v = x;
        var functionEvaluations = 0;
        if (!TryEvaluate(
                function,
                x,
                ref functionEvaluations,
                out var fx)) {
            result = OptimizationResult.Failed(0);
            report = CreateScalarReport(
                OptimizationMethod.Brent,
                OptimizationStatus.FunctionEvaluationFailed,
                0,
                functionEvaluations,
                double.NaN,
                double.NaN);
            return false;
        }

        var fw = fx;
        var fv = fx;
        var step = 0.0;
        var previousStep = 0.0;
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            var middle = 0.5 * (a + b);
            var tolerance = (relativeTolerance * Math.Abs(x)) + absoluteTolerance;
            var twiceTolerance = 2 * tolerance;
            if (Math.Abs(x - middle) <=
                twiceTolerance - (0.5 * (b - a))) {
                result = new OptimizationResult(x, fx, iteration, true);
                report = CreateScalarReport(
                    OptimizationMethod.Brent,
                    OptimizationStatus.Succeeded,
                    iteration,
                    functionEvaluations,
                    fx,
                    Math.Abs(b - a));
                return true;
            }

            var useParabolicStep = false;
            if (Math.Abs(previousStep) > tolerance) {
                var r = (x - w) * (fx - fv);
                var q = (x - v) * (fx - fw);
                var p = (x - v) * q - (x - w) * r;
                q = 2 * (q - r);
                if (q > 0) {
                    p = -p;
                }

                q = Math.Abs(q);
                var previous = previousStep;
                previousStep = step;
                if (Math.Abs(p) < Math.Abs(0.5 * q * previous) &&
                    p > q * (a - x) &&
                    p < q * (b - x)) {
                    step = p / q;
                    var candidate = x + step;
                    if (candidate - a < twiceTolerance ||
                        b - candidate < twiceTolerance) {
                        step = Math.CopySign(tolerance, middle - x);
                    }

                    useParabolicStep = true;
                }
            }

            if (!useParabolicStep) {
                previousStep = x >= middle ? a - x : b - x;
                step = GoldenRatio * previousStep;
            }

            var next = x + step;
            if (!double.IsFinite(next)) {
                result = OptimizationResult.Failed(iteration);
                report = CreateScalarReport(
                    OptimizationMethod.Brent,
                    OptimizationStatus.NonFiniteStep,
                    iteration,
                    functionEvaluations,
                    fx,
                    step);
                return false;
            }

            if (!TryEvaluate(
                    function,
                    next,
                    ref functionEvaluations,
                    out var nextValue)) {
                result = OptimizationResult.Failed(iteration);
                report = CreateScalarReport(
                    OptimizationMethod.Brent,
                    OptimizationStatus.FunctionEvaluationFailed,
                    iteration,
                    functionEvaluations,
                    fx,
                    step);
                return false;
            }

            if (nextValue <= fx) {
                if (next >= x) {
                    a = x;
                } else {
                    b = x;
                }

                v = w;
                fv = fw;
                w = x;
                fw = fx;
                x = next;
                fx = nextValue;
            } else {
                if (next < x) {
                    a = next;
                } else {
                    b = next;
                }

                if (nextValue <= fw || w == x) {
                    v = w;
                    fv = fw;
                    w = next;
                    fw = nextValue;
                } else if (nextValue <= fv || v == x || v == w) {
                    v = next;
                    fv = nextValue;
                }
            }
        }

        result = OptimizationResult.Failed(maximumIterations);
        report = CreateScalarReport(
            OptimizationMethod.Brent,
            OptimizationStatus.MaximumIterationsReached,
            maximumIterations,
            functionEvaluations,
            fx,
            Math.Abs(b - a));
        return false;
    }

    /// <summary>
    /// Performs gradient descent with finite-difference gradients and
    /// backtracking. The point buffer is updated in place.
    /// </summary>
    public static bool TryGradientDescent(
        SpanFunction objective,
        Span<double> point,
        double initialStepSize,
        int maximumIterations,
        Span<double> gradient,
        Span<double> candidate,
        double gradientTolerance = 1e-8,
        double relativeStep = 1e-5) {
        return TryGradientDescent(
            objective,
            point,
            initialStepSize,
            maximumIterations,
            gradient,
            candidate,
            out _,
            out _,
            gradientTolerance,
            relativeStep);
    }

    /// <summary>
    /// Attempts finite-difference gradient descent and reports the numerical
    /// termination reason.
    /// </summary>
    public static bool TryGradientDescent(
        SpanFunction objective,
        Span<double> point,
        double initialStepSize,
        int maximumIterations,
        Span<double> gradient,
        Span<double> candidate,
        out MultivariateOptimizationResult result,
        out OptimizationReport report,
        double gradientTolerance = 1e-8,
        double relativeStep = 1e-5) {
        ArgumentNullException.ThrowIfNull(objective);
        if (point.Length == 0) {
            throw new ArgumentException(
                "The point must contain at least one coordinate.",
                nameof(point));
        }

        if (gradient.Length < point.Length || candidate.Length < point.Length) {
            throw new ArgumentException(
                "The gradient and candidate buffers must match the point length.",
                nameof(gradient));
        }

        if (point.Overlaps(gradient) ||
            point.Overlaps(candidate) ||
            gradient.Overlaps(candidate)) {
            throw new ArgumentException(
                "The point and work buffers must not overlap.",
                nameof(gradient));
        }

        if (!double.IsFinite(initialStepSize) || initialStepSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(initialStepSize));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIterations);
        if (!double.IsFinite(gradientTolerance) || gradientTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(gradientTolerance));
        }

        if (!double.IsFinite(relativeStep) || relativeStep <= 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeStep));
        }

        var functionEvaluations = 0;
        if (!TryEvaluate(
                objective,
                point,
                ref functionEvaluations,
                out var currentValue)) {
            result = FailedMultivariate(0, functionEvaluations);
            report = CreateReport(
                OptimizationMethod.GradientDescent,
                OptimizationStatus.FunctionEvaluationFailed,
                0,
                functionEvaluations,
                double.NaN,
                double.NaN);
            return false;
        }

        var stepSize = initialStepSize;
        var gradientNorm = double.NaN;
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            if (!TryFiniteDifferenceGradient(
                objective,
                point,
                gradient,
                candidate,
                relativeStep,
                ref functionEvaluations)) {
                result = FailedMultivariate(
                    iteration - 1,
                    functionEvaluations);
                report = CreateReport(
                    OptimizationMethod.GradientDescent,
                    OptimizationStatus.GradientEvaluationFailed,
                    iteration - 1,
                    functionEvaluations,
                    currentValue,
                    gradientNorm);
                return false;
            }

            gradientNorm = EuclideanNorm(gradient[..point.Length]);
            if (gradientNorm <= gradientTolerance) {
                result = new MultivariateOptimizationResult(
                    currentValue,
                    iteration - 1,
                    functionEvaluations,
                    true);
                report = CreateReport(
                    OptimizationMethod.GradientDescent,
                    OptimizationStatus.Succeeded,
                    iteration - 1,
                    functionEvaluations,
                    currentValue,
                    gradientNorm,
                    stepSize);
                return true;
            }

            var accepted = false;
            var sawFiniteCandidateValue = false;
            for (var retry = 0; retry < MaximumBacktrackingSteps; retry++) {
                var finiteCandidate = true;
                for (var index = 0; index < point.Length; index++) {
                    candidate[index] = point[index] - (stepSize * gradient[index]);
                    if (!double.IsFinite(candidate[index])) {
                        finiteCandidate = false;
                        break;
                    }
                }

                if (!finiteCandidate) {
                    stepSize *= 0.5;
                    continue;
                }

                if (!TryEvaluate(
                        objective,
                        candidate[..point.Length],
                        ref functionEvaluations,
                        out var candidateValue)) {
                    stepSize *= 0.5;
                    continue;
                }

                sawFiniteCandidateValue = true;
                if (candidateValue <= currentValue) {
                    candidate[..point.Length].CopyTo(point);
                    currentValue = candidateValue;
                    stepSize *= 1.1;
                    accepted = true;
                    break;
                }

                stepSize *= 0.5;
            }

            if (!accepted) {
                result = FailedMultivariate(
                    iteration - 1,
                    functionEvaluations);
                report = CreateReport(
                    OptimizationMethod.GradientDescent,
                    sawFiniteCandidateValue
                        ? OptimizationStatus.LineSearchFailed
                        : OptimizationStatus.FunctionEvaluationFailed,
                    iteration - 1,
                    functionEvaluations,
                    currentValue,
                    gradientNorm,
                    stepSize);
                return false;
            }
        }

        result = new MultivariateOptimizationResult(
            currentValue,
            maximumIterations,
            functionEvaluations,
            false);
        report = CreateReport(
            OptimizationMethod.GradientDescent,
            OptimizationStatus.MaximumIterationsReached,
            maximumIterations,
            functionEvaluations,
            currentValue,
            gradientNorm,
            stepSize);
        return false;
    }

    private static void ValidateBracket(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        ArgumentNullException.ThrowIfNull(function);
        if (!double.IsFinite(lower) || !double.IsFinite(upper)) {
            throw new ArgumentOutOfRangeException(nameof(lower));
        }

        if (lower >= upper) {
            throw new ArgumentException(
                "The lower bound must be less than the upper bound.",
                nameof(upper));
        }

        if (!double.IsFinite(absoluteTolerance) || absoluteTolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(absoluteTolerance));
        }

        if (!double.IsFinite(relativeTolerance) || relativeTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIterations);
    }

    private static bool TryEvaluate(
        Func<double, double> function,
        double value,
        ref int functionEvaluations,
        out double result) {
        functionEvaluations++;
        result = function(value);
        return double.IsFinite(result);
    }

    private static bool TryEvaluate(
        SpanFunction function,
        ReadOnlySpan<double> point,
        ref int functionEvaluations,
        out double result) {
        functionEvaluations++;
        result = function(point);
        return double.IsFinite(result);
    }

    private static bool TryFiniteDifferenceGradient(
        SpanFunction function,
        ReadOnlySpan<double> point,
        Span<double> gradient,
        Span<double> workspace,
        double relativeStep,
        ref int functionEvaluations) {
        for (var index = 0; index < point.Length; index++) {
            var step = relativeStep * Math.Max(1, Math.Abs(point[index]));
            var original = point[index];
            workspace[index] = original + step;
            if (!TryEvaluate(
                    function,
                    workspace[..point.Length],
                    ref functionEvaluations,
                    out var upper)) {
                gradient[..point.Length].Clear();
                workspace[index] = original;
                return false;
            }

            workspace[index] = original - step;
            if (!TryEvaluate(
                    function,
                    workspace[..point.Length],
                    ref functionEvaluations,
                    out var lower)) {
                gradient[..point.Length].Clear();
                workspace[index] = original;
                return false;
            }

            workspace[index] = original;
            gradient[index] = (upper - lower) / (2 * step);
            if (!double.IsFinite(gradient[index])) {
                gradient[..point.Length].Clear();
                return false;
            }
        }

        return true;
    }

    private static double EuclideanNorm(ReadOnlySpan<double> values) {
        var scale = 0.0;
        for (var index = 0; index < values.Length; index++) {
            scale = Math.Max(scale, Math.Abs(values[index]));
        }

        if (scale == 0) {
            return 0;
        }

        var sum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            var scaled = values[index] / scale;
            sum += scaled * scaled;
        }

        return scale * Math.Sqrt(sum);
    }

    private static OptimizationReport CreateScalarReport(
        OptimizationMethod method,
        OptimizationStatus status,
        int iterations,
        int functionEvaluations,
        double objectiveValue,
        double stepSize) {
        return CreateReport(
            method,
            status,
            iterations,
            functionEvaluations,
            objectiveValue,
            double.NaN,
            stepSize);
    }

    private static OptimizationReport CreateReport(
        OptimizationMethod method,
        OptimizationStatus status,
        int iterations,
        int functionEvaluations,
        double objectiveValue,
        double gradientNorm,
        double stepSize = double.NaN) {
        return new OptimizationReport(
            method,
            status,
            iterations,
            functionEvaluations,
            objectiveValue,
            gradientNorm,
            stepSize,
            double.NaN);
    }

    private static void ThrowIfFunctionEvaluationFailed(OptimizationReport report) {
        if (report.Status == OptimizationStatus.FunctionEvaluationFailed) {
            throw new ArithmeticException(
                "The objective function returned a non-finite value.");
        }
    }
}
