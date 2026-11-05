// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Evaluates a real function of a read-only numeric vector without requiring
/// an allocation at the call boundary.
/// </summary>
public delegate double SpanFunction(ReadOnlySpan<double> value);

/// <summary>
/// Finite-difference differentiation helpers for scalar and multivariate
/// functions.
/// </summary>
public static partial class NumericalDifferentiation {
    private const double DefaultRelativeStep = 1e-5;

    public static double ForwardDifference(
        Func<double, double> function,
        double value,
        double step) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateStep(value, step);
        return EnsureFinite(
            (Evaluate(function, value + step) - Evaluate(function, value)) / step);
    }

    public static double BackwardDifference(
        Func<double, double> function,
        double value,
        double step) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateStep(value, step);
        return EnsureFinite(
            (Evaluate(function, value) - Evaluate(function, value - step)) / step);
    }

    public static double CentralDifference(
        Func<double, double> function,
        double value,
        double? step = null) {
        ArgumentNullException.ThrowIfNull(function);
        var actualStep = ResolveStep(value, step);
        return EnsureFinite(
            (Evaluate(function, value + actualStep) -
                Evaluate(function, value - actualStep)) /
            (2 * actualStep));
    }

    /// <summary>
    /// Evaluates a fourth-order central difference.
    /// </summary>
    public static double CentralDifference4(
        Func<double, double> function,
        double value,
        double? step = null) {
        ArgumentNullException.ThrowIfNull(function);
        var actualStep = ResolveStep(value, step);
        var twiceStep = 2 * actualStep;
        return EnsureFinite(
            ((-Evaluate(function, value + twiceStep)) +
                (8 * Evaluate(function, value + actualStep)) -
                (8 * Evaluate(function, value - actualStep)) +
                Evaluate(function, value - twiceStep)) /
            (12 * actualStep));
    }

    /// <summary>
    /// Evaluates the second derivative with a central second difference.
    /// </summary>
    public static double CentralSecondDifference(
        Func<double, double> function,
        double value,
        double? step = null) {
        ArgumentNullException.ThrowIfNull(function);
        var actualStep = ResolveStep(value, step);
        return EnsureFinite(
            (Evaluate(function, value + actualStep) -
                (2 * Evaluate(function, value)) +
                Evaluate(function, value - actualStep)) /
            (actualStep * actualStep));
    }

    /// <summary>
    /// Computes a central-difference gradient.
    /// </summary>
    /// <param name="function">The function to differentiate.</param>
    /// <param name="point">The finite evaluation point.</param>
    /// <param name="gradient">Receives the gradient components.</param>
    /// <param name="workspace">At least <c>point.Length</c> scratch values.</param>
    /// <param name="relativeStep">The positive relative coordinate step.</param>
    /// <returns><see langword="true"/> when all gradient components are finite.</returns>
    public static bool TryGradient(
        SpanFunction function,
        ReadOnlySpan<double> point,
        Span<double> gradient,
        Span<double> workspace,
        double relativeStep = DefaultRelativeStep) {
        return TryGradient(
            function,
            point,
            gradient,
            workspace,
            out _,
            relativeStep);
    }

    /// <summary>
    /// Computes a central-difference gradient and returns a diagnostic report.
    /// </summary>
    /// <param name="function">The function to differentiate.</param>
    /// <param name="point">The finite evaluation point.</param>
    /// <param name="gradient">Receives the gradient components.</param>
    /// <param name="workspace">At least <c>point.Length</c> scratch values.</param>
    /// <param name="report">Receives the termination status and evaluation count.</param>
    /// <param name="relativeStep">
    /// Positive relative coordinate step. Each coordinate step is
    /// <c>relativeStep * max(1, |x_i|)</c>.
    /// </param>
    /// <returns><see langword="true"/> when all gradient components are finite.</returns>
    /// <remarks>
    /// Shape, overlap, and step-domain violations throw. Non-finite input
    /// coordinates, callback values, and derivative combinations are returned
    /// through <paramref name="report"/>. On failure the gradient destination
    /// is cleared. No allocation occurs after validation.
    /// </remarks>
    public static bool TryGradient(
        SpanFunction function,
        ReadOnlySpan<double> point,
        Span<double> gradient,
        Span<double> workspace,
        out NumericalDifferentiationMultivariateReport report,
        double relativeStep = DefaultRelativeStep) {
        ArgumentNullException.ThrowIfNull(function);
        if (point.Length == 0) {
            throw new ArgumentException(
                "The point must contain at least one coordinate.",
                nameof(point));
        }

        if (gradient.Length < point.Length) {
            throw new ArgumentException(
                "The gradient destination is too small.",
                nameof(gradient));
        }

        if (workspace.Length < point.Length) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (gradient.Overlaps(workspace) || point.Overlaps(gradient) ||
            point.Overlaps(workspace)) {
            throw new ArgumentException(
                "The point, gradient, and workspace must not overlap.",
                nameof(workspace));
        }

        ValidateRelativeStep(relativeStep);
        var result = gradient[..point.Length];
        for (var index = 0; index < point.Length; index++) {
            if (!double.IsFinite(point[index])) {
                result.Clear();
                report = CreateMultivariateReport(
                    NumericalDifferentiationMethod.CentralGradient,
                    NumericalDifferentiationStatus.NonFiniteInput,
                    relativeStep,
                    0);
                return false;
            }
        }

        point.CopyTo(workspace);
        var evaluationCount = 0;
        for (var index = 0; index < point.Length; index++) {
            var step = relativeStep * Math.Max(1, Math.Abs(point[index]));
            var original = point[index];
            workspace[index] = original + step;
            var upper = function(workspace);
            evaluationCount++;
            workspace[index] = original - step;
            var lower = function(workspace);
            evaluationCount++;
            workspace[index] = original;
            if (!double.IsFinite(upper) || !double.IsFinite(lower)) {
                result.Clear();
                report = CreateMultivariateReport(
                    NumericalDifferentiationMethod.CentralGradient,
                    NumericalDifferentiationStatus.NonFiniteFunctionValue,
                    relativeStep,
                    evaluationCount);
                return false;
            }

            var derivative = (upper - lower) / (2 * step);
            if (!double.IsFinite(derivative)) {
                result.Clear();
                report = CreateMultivariateReport(
                    NumericalDifferentiationMethod.CentralGradient,
                    NumericalDifferentiationStatus.NonFiniteDerivative,
                    relativeStep,
                    evaluationCount);
                return false;
            }

            result[index] = derivative;
        }

        report = CreateMultivariateReport(
            NumericalDifferentiationMethod.CentralGradient,
            NumericalDifferentiationStatus.Succeeded,
            relativeStep,
            evaluationCount);
        return true;
    }

    /// <summary>
    /// Computes a central-difference Hessian of a real scalar function.
    /// </summary>
    /// <param name="function">The function to differentiate.</param>
    /// <param name="point">The finite evaluation point.</param>
    /// <param name="hessian">
    /// Row-major destination with at least <c>n * n</c> values, where <c>n</c>
    /// is the number of coordinates.
    /// </param>
    /// <param name="workspace">Scratch storage with at least <c>n</c> values.</param>
    /// <param name="relativeStep">The positive relative coordinate step.</param>
    /// <returns><see langword="true"/> when the Hessian is finite.</returns>
    public static bool TryHessian(
        SpanFunction function,
        ReadOnlySpan<double> point,
        Span<double> hessian,
        Span<double> workspace,
        double relativeStep = DefaultRelativeStep) {
        return TryHessian(
            function,
            point,
            hessian,
            workspace,
            out _,
            relativeStep);
    }

    /// <summary>
    /// Computes a central-difference Hessian of a real scalar function and
    /// returns a diagnostic report.
    /// </summary>
    /// <param name="function">The function to differentiate.</param>
    /// <param name="point">The finite evaluation point.</param>
    /// <param name="hessian">
    /// Row-major destination with at least <c>n * n</c> values, where <c>n</c>
    /// is <c>point.Length</c>.
    /// </param>
    /// <param name="workspace">Scratch storage with at least <c>n</c> values.</param>
    /// <param name="report">Receives the termination status and evaluation count.</param>
    /// <param name="relativeStep">
    /// Positive relative coordinate step. Each coordinate step is
    /// <c>relativeStep * max(1, |x_i|)</c>.
    /// </param>
    /// <remarks>
    /// Diagonal entries use a three-point second difference. Off-diagonal
    /// entries use the four-point cross difference and are written
    /// symmetrically. Shape, overlap, and step-domain violations throw.
    /// Non-finite input coordinates, callback values, and derivative
    /// combinations are returned through <paramref name="report"/>. On failure
    /// the Hessian destination is cleared. No allocation occurs after
    /// validation.
    /// </remarks>
    public static bool TryHessian(
        SpanFunction function,
        ReadOnlySpan<double> point,
        Span<double> hessian,
        Span<double> workspace,
        out NumericalDifferentiationMultivariateReport report,
        double relativeStep = DefaultRelativeStep) {
        ArgumentNullException.ThrowIfNull(function);
        if (point.IsEmpty) {
            throw new ArgumentException(
                "The point must contain at least one coordinate.",
                nameof(point));
        }

        var elementCount = checked(point.Length * point.Length);
        if (hessian.Length < elementCount) {
            throw new ArgumentException(
                "The Hessian destination is too small.",
                nameof(hessian));
        }

        if (workspace.Length < point.Length) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (hessian.Overlaps(workspace) ||
            point.Overlaps(hessian) ||
            point.Overlaps(workspace)) {
            throw new ArgumentException(
                "The point, Hessian, and workspace must not overlap.",
                nameof(workspace));
        }

        ValidateRelativeStep(relativeStep);
        var result = hessian[..elementCount];
        for (var index = 0; index < point.Length; index++) {
            if (!double.IsFinite(point[index])) {
                result.Clear();
                report = CreateMultivariateReport(
                    NumericalDifferentiationMethod.CentralHessian,
                    NumericalDifferentiationStatus.NonFiniteInput,
                    relativeStep,
                    0);
                return false;
            }
        }

        point.CopyTo(workspace);
        var center = function(workspace);
        var evaluationCount = 1;
        if (!double.IsFinite(center)) {
            result.Clear();
            report = CreateMultivariateReport(
                NumericalDifferentiationMethod.CentralHessian,
                NumericalDifferentiationStatus.NonFiniteFunctionValue,
                relativeStep,
                evaluationCount);
            return false;
        }

        for (var row = 0; row < point.Length; row++) {
            var rowStep = relativeStep * Math.Max(1, Math.Abs(point[row]));
            var originalRow = point[row];
            workspace[row] = originalRow + rowStep;
            var upper = function(workspace);
            evaluationCount++;
            workspace[row] = originalRow - rowStep;
            var lower = function(workspace);
            evaluationCount++;
            workspace[row] = originalRow;
            if (!double.IsFinite(upper) || !double.IsFinite(lower)) {
                result.Clear();
                report = CreateMultivariateReport(
                    NumericalDifferentiationMethod.CentralHessian,
                    NumericalDifferentiationStatus.NonFiniteFunctionValue,
                    relativeStep,
                    evaluationCount);
                return false;
            }

            var diagonal = (upper - (2 * center) + lower) /
                (rowStep * rowStep);
            if (!double.IsFinite(diagonal)) {
                result.Clear();
                report = CreateMultivariateReport(
                    NumericalDifferentiationMethod.CentralHessian,
                    NumericalDifferentiationStatus.NonFiniteDerivative,
                    relativeStep,
                    evaluationCount);
                return false;
            }

            result[(row * point.Length) + row] = diagonal;
            for (var column = row + 1; column < point.Length; column++) {
                var columnStep = relativeStep * Math.Max(
                    1,
                    Math.Abs(point[column]));
                var originalColumn = point[column];
                workspace[row] = originalRow + rowStep;
                workspace[column] = originalColumn + columnStep;
                var upperUpper = function(workspace);
                evaluationCount++;
                workspace[column] = originalColumn - columnStep;
                var upperLower = function(workspace);
                evaluationCount++;
                workspace[row] = originalRow - rowStep;
                workspace[column] = originalColumn + columnStep;
                var lowerUpper = function(workspace);
                evaluationCount++;
                workspace[column] = originalColumn - columnStep;
                var lowerLower = function(workspace);
                evaluationCount++;
                workspace[row] = originalRow;
                workspace[column] = originalColumn;
                if (!double.IsFinite(upperUpper) ||
                    !double.IsFinite(upperLower) ||
                    !double.IsFinite(lowerUpper) ||
                    !double.IsFinite(lowerLower)) {
                    result.Clear();
                    report = CreateMultivariateReport(
                        NumericalDifferentiationMethod.CentralHessian,
                        NumericalDifferentiationStatus.NonFiniteFunctionValue,
                        relativeStep,
                        evaluationCount);
                    return false;
                }

                var mixed = (upperUpper - upperLower - lowerUpper + lowerLower) /
                    (4 * rowStep * columnStep);
                if (!double.IsFinite(mixed)) {
                    result.Clear();
                    report = CreateMultivariateReport(
                        NumericalDifferentiationMethod.CentralHessian,
                        NumericalDifferentiationStatus.NonFiniteDerivative,
                        relativeStep,
                        evaluationCount);
                    return false;
                }

                result[(row * point.Length) + column] = mixed;
                result[(column * point.Length) + row] = mixed;
            }
        }

        report = CreateMultivariateReport(
            NumericalDifferentiationMethod.CentralHessian,
            NumericalDifferentiationStatus.Succeeded,
            relativeStep,
            evaluationCount);
        return true;
    }

    private static NumericalDifferentiationMultivariateReport CreateMultivariateReport(
        NumericalDifferentiationMethod method,
        NumericalDifferentiationStatus status,
        double relativeStep,
        int functionEvaluationCount) {
        return new NumericalDifferentiationMultivariateReport(
            method,
            status,
            relativeStep,
            functionEvaluationCount);
    }

    private static double ResolveStep(double value, double? step) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var actualStep = step ??
            DefaultRelativeStep * Math.Max(1, Math.Abs(value));
        ValidateStep(value, actualStep);
        return actualStep;
    }

    private static void ValidateStep(double value, double step) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (!double.IsFinite(step) || step <= 0) {
            throw new ArgumentOutOfRangeException(nameof(step));
        }
    }

    private static void ValidateRelativeStep(double relativeStep) {
        if (!double.IsFinite(relativeStep) || relativeStep <= 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeStep));
        }
    }

    private static double Evaluate(Func<double, double> function, double value) {
        var result = function(value);
        if (!double.IsFinite(result)) {
            throw new ArithmeticException(
                "The differentiated function returned a non-finite value.");
        }

        return result;
    }

    private static double EnsureFinite(double value) {
        if (!double.IsFinite(value)) {
            throw new ArithmeticException(
                "The numerical derivative is non-finite.");
        }

        return value;
    }
}
