// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Evaluates an objective gradient into a caller-provided destination.
/// </summary>
/// <returns>
/// <see langword="true"/> when the gradient is finite and usable.
/// </returns>
public delegate bool SpanGradientFunction(
    ReadOnlySpan<double> point,
    Span<double> gradient);

/// <summary>
/// The outcome of a multivariate minimization.
/// </summary>
public readonly record struct MultivariateOptimizationResult {
    public MultivariateOptimizationResult(
        double value,
        int iterations,
        int functionEvaluations,
        bool converged) {
        Value = value;
        Iterations = iterations;
        FunctionEvaluations = functionEvaluations;
        Converged = converged;
    }

    public double Value { get; }

    public int Iterations { get; }

    public int FunctionEvaluations { get; }

    public bool Converged { get; }
}

public static partial class Optimization {
    private const double ArmijoSufficientDecrease = 1e-4;
    private const double CurvatureRelativeTolerance = 1e-12;

    /// <summary>
    /// Minimizes a multivariate objective with the BFGS quasi-Newton method.
    /// </summary>
    /// <remarks>
    /// The point is updated in place. The inverse-Hessian approximation is
    /// caller-owned and must contain <c>order * order</c> values. The workspace
    /// must contain at least <c>7 * order</c> values. Both are initialized by
    /// this method. The line search uses an Armijo sufficient-decrease test.
    /// Convergence is based on the infinity norm of the gradient, not on an
    /// objective-value threshold. The BFGS update was independently introduced
    /// by Broyden (1970),
    /// <see href="https://doi.org/10.1093/imamat/6.1.76">doi:10.1093/imamat/6.1.76</see>,
    /// Fletcher (1970),
    /// <see href="https://doi.org/10.1093/comjnl/13.3.317">doi:10.1093/comjnl/13.3.317</see>,
    /// Goldfarb (1970),
    /// <see href="https://doi.org/10.1090/S0025-5718-1970-0258249-6">doi:10.1090/S0025-5718-1970-0258249-6</see>,
    /// and Shanno (1970),
    /// <see href="https://doi.org/10.1090/S0025-5718-1970-0274029-X">doi:10.1090/S0025-5718-1970-0274029-X</see>.
    /// </remarks>
    public static bool TryBfgs(
        SpanFunction objective,
        SpanGradientFunction objectiveGradient,
        Span<double> point,
        Span<double> inverseHessian,
        Span<double> workspace,
        out MultivariateOptimizationResult result,
        double gradientTolerance = 1e-8,
        int maximumIterations = 500,
        int maximumLineSearchSteps = 60) {
        return TryBfgs(
            objective,
            objectiveGradient,
            point,
            inverseHessian,
            workspace,
            out result,
            out _,
            gradientTolerance,
            maximumIterations,
            maximumLineSearchSteps);
    }

    /// <summary>
    /// Attempts BFGS minimization and reports the numerical termination reason.
    /// </summary>
    public static bool TryBfgs(
        SpanFunction objective,
        SpanGradientFunction objectiveGradient,
        Span<double> point,
        Span<double> inverseHessian,
        Span<double> workspace,
        out MultivariateOptimizationResult result,
        out OptimizationReport report,
        double gradientTolerance = 1e-8,
        int maximumIterations = 500,
        int maximumLineSearchSteps = 60) {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(objectiveGradient);
        var order = point.Length;
        if (order == 0) {
            throw new ArgumentException(
                "The point must contain at least one coordinate.",
                nameof(point));
        }

        var hessianCount = checked(order * order);
        var workspaceCount = checked(7 * order);
        if (inverseHessian.Length < hessianCount) {
            throw new ArgumentException(
                "The inverse-Hessian buffer must contain order * order values.",
                nameof(inverseHessian));
        }

        if (workspace.Length < workspaceCount) {
            throw new ArgumentException(
                "The workspace must contain at least seven values per coordinate.",
                nameof(workspace));
        }

        if (point.Overlaps(inverseHessian) || point.Overlaps(workspace)) {
            throw new ArgumentException(
                "The point must not overlap a work buffer.",
                nameof(point));
        }

        if (inverseHessian.Overlaps(workspace)) {
            throw new ArgumentException(
                "The work buffers must not overlap.",
                nameof(workspace));
        }

        if (!double.IsFinite(gradientTolerance) || gradientTolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(gradientTolerance));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLineSearchSteps);
        var gradient = workspace[..order];
        var nextGradient = workspace.Slice(order, order);
        var direction = workspace.Slice(order * 2, order);
        var candidate = workspace.Slice(order * 3, order);
        var step = workspace.Slice(order * 4, order);
        var gradientDifference = workspace.Slice(order * 5, order);
        var hessianTimesGradient = workspace.Slice(order * 6, order);
        var hessian = inverseHessian[..hessianCount];
        hessian.Clear();
        for (var index = 0; index < order; index++) {
            hessian[(index * order) + index] = 1;
        }

        var functionEvaluations = 0;
        var value = EvaluateObjective(objective, point, ref functionEvaluations);
        if (!double.IsFinite(value)) {
            result = FailedMultivariate(0, functionEvaluations);
            report = CreateReport(
                OptimizationMethod.Bfgs,
                OptimizationStatus.FunctionEvaluationFailed,
                0,
                functionEvaluations,
                value,
                double.NaN);
            return false;
        }

        if (!EvaluateGradient(objectiveGradient, point, gradient)) {
            result = FailedMultivariate(0, functionEvaluations);
            report = CreateReport(
                OptimizationMethod.Bfgs,
                OptimizationStatus.GradientEvaluationFailed,
                0,
                functionEvaluations,
                value,
                double.NaN);
            return false;
        }

        var gradientNorm = InfinityNorm(gradient);
        var lastStepSize = double.NaN;
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            if (gradientNorm <= gradientTolerance) {
                result = new MultivariateOptimizationResult(
                    value,
                    iteration - 1,
                    functionEvaluations,
                    true);
                report = CreateReport(
                    OptimizationMethod.Bfgs,
                    OptimizationStatus.Succeeded,
                    iteration - 1,
                    functionEvaluations,
                    value,
                    gradientNorm,
                    lastStepSize);
                return true;
            }

            ComputeDirection(hessian, gradient, direction, order);
            var directionalDerivative = Dot(gradient, direction);
            if (!double.IsFinite(directionalDerivative) || directionalDerivative >= 0) {
                ResetInverseHessian(hessian, order);
                for (var index = 0; index < order; index++) {
                    direction[index] = -gradient[index];
                }

                directionalDerivative = Dot(gradient, direction);
            }

            if (!double.IsFinite(directionalDerivative)) {
                result = FailedMultivariate(
                    iteration - 1,
                    functionEvaluations);
                report = CreateReport(
                    OptimizationMethod.Bfgs,
                    OptimizationStatus.NonFiniteStep,
                    iteration - 1,
                    functionEvaluations,
                    value,
                    gradientNorm,
                    lastStepSize);
                return false;
            }

            if (directionalDerivative >= 0) {
                result = FailedMultivariate(
                    iteration - 1,
                    functionEvaluations);
                report = CreateReport(
                    OptimizationMethod.Bfgs,
                    OptimizationStatus.NonPositiveCurvature,
                    iteration - 1,
                    functionEvaluations,
                    value,
                    gradientNorm,
                    lastStepSize);
                return false;
            }

            var accepted = false;
            var stepSize = 1.0;
            var candidateValue = double.NaN;
            var sawFiniteCandidateValue = false;
            for (var searchStep = 0; searchStep < maximumLineSearchSteps; searchStep++) {
                var finiteCandidate = true;
                for (var index = 0; index < order; index++) {
                    var coordinate = point[index] + (stepSize * direction[index]);
                    candidate[index] = coordinate;
                    if (!double.IsFinite(coordinate)) {
                        finiteCandidate = false;
                        break;
                    }
                }

                if (!finiteCandidate) {
                    stepSize *= 0.5;
                    continue;
                }

                candidateValue = EvaluateObjective(
                    objective,
                    candidate,
                    ref functionEvaluations);
                if (!double.IsFinite(candidateValue)) {
                    stepSize *= 0.5;
                    continue;
                }

                sawFiniteCandidateValue = true;
                if (candidateValue <= value +
                    (ArmijoSufficientDecrease * stepSize * directionalDerivative)) {
                    accepted = true;
                    break;
                }

                stepSize *= 0.5;
            }

            lastStepSize = stepSize;
            if (!accepted) {
                result = FailedMultivariate(iteration - 1, functionEvaluations);
                report = CreateReport(
                    OptimizationMethod.Bfgs,
                    sawFiniteCandidateValue
                        ? OptimizationStatus.LineSearchFailed
                        : OptimizationStatus.FunctionEvaluationFailed,
                    iteration - 1,
                    functionEvaluations,
                    value,
                    gradientNorm,
                    stepSize);
                return false;
            }

            if (!EvaluateGradient(objectiveGradient, candidate, nextGradient)) {
                result = FailedMultivariate(iteration - 1, functionEvaluations);
                report = CreateReport(
                    OptimizationMethod.Bfgs,
                    OptimizationStatus.GradientEvaluationFailed,
                    iteration - 1,
                    functionEvaluations,
                    value,
                    gradientNorm,
                    stepSize);
                return false;
            }

            for (var index = 0; index < order; index++) {
                step[index] = candidate[index] - point[index];
                gradientDifference[index] = nextGradient[index] - gradient[index];
            }

            candidate[..order].CopyTo(point);
            nextGradient[..order].CopyTo(gradient);
            value = candidateValue;
            gradientNorm = InfinityNorm(gradient);
            UpdateInverseHessian(
                hessian,
                step,
                gradientDifference,
                hessianTimesGradient,
                order);
        }

        var converged = gradientNorm <= gradientTolerance;
        result = new MultivariateOptimizationResult(
            value,
            maximumIterations,
            functionEvaluations,
            converged);
        report = CreateReport(
            OptimizationMethod.Bfgs,
            converged
                ? OptimizationStatus.Succeeded
                : OptimizationStatus.MaximumIterationsReached,
            maximumIterations,
            functionEvaluations,
            value,
            gradientNorm,
            lastStepSize);
        return converged;
    }

    private static void ComputeDirection(
        ReadOnlySpan<double> inverseHessian,
        ReadOnlySpan<double> gradient,
        Span<double> direction,
        int order) {
        for (var row = 0; row < order; row++) {
            var value = 0.0;
            var rowOffset = row * order;
            for (var column = 0; column < order; column++) {
                value += inverseHessian[rowOffset + column] * gradient[column];
            }

            direction[row] = -value;
        }
    }

    private static void UpdateInverseHessian(
        Span<double> inverseHessian,
        ReadOnlySpan<double> step,
        ReadOnlySpan<double> gradientDifference,
        Span<double> hessianTimesGradient,
        int order) {
        double stepGradient = 0;
        double stepSquared = 0;
        double gradientSquared = 0;
        for (var index = 0; index < order; index++) {
            stepGradient += step[index] * gradientDifference[index];
            stepSquared += step[index] * step[index];
            gradientSquared += gradientDifference[index] * gradientDifference[index];
        }

        if (!double.IsFinite(stepGradient) ||
            stepGradient <= CurvatureRelativeTolerance *
                Math.Sqrt(stepSquared * gradientSquared)) {
            return;
        }

        for (var row = 0; row < order; row++) {
            var value = 0.0;
            var rowOffset = row * order;
            for (var column = 0; column < order; column++) {
                value += inverseHessian[rowOffset + column] *
                    gradientDifference[column];
            }

            hessianTimesGradient[row] = value;
        }

        var gradientHessianGradient = Dot(gradientDifference, hessianTimesGradient);
        var stepScale = (stepGradient + gradientHessianGradient) /
            (stepGradient * stepGradient);
        var crossScale = 1 / stepGradient;
        for (var row = 0; row < order; row++) {
            var rowOffset = row * order;
            for (var column = 0; column <= row; column++) {
                var value = inverseHessian[rowOffset + column] +
                    (stepScale * step[row] * step[column]) -
                    (crossScale * ((hessianTimesGradient[row] * step[column]) +
                        (step[row] * hessianTimesGradient[column])));
                inverseHessian[rowOffset + column] = value;
                inverseHessian[(column * order) + row] = value;
            }
        }
    }

    private static void ResetInverseHessian(Span<double> inverseHessian, int order) {
        inverseHessian.Clear();
        for (var index = 0; index < order; index++) {
            inverseHessian[(index * order) + index] = 1;
        }
    }

    private static bool EvaluateGradient(
        SpanGradientFunction objectiveGradient,
        ReadOnlySpan<double> point,
        Span<double> gradient) {
        if (!objectiveGradient(point, gradient)) {
            return false;
        }

        for (var index = 0; index < point.Length; index++) {
            if (!double.IsFinite(gradient[index])) {
                return false;
            }
        }

        return true;
    }

    private static double EvaluateObjective(
        SpanFunction objective,
        ReadOnlySpan<double> point,
        ref int functionEvaluations) {
        functionEvaluations++;
        return objective(point);
    }

    private static bool IsFinite(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second) {
        for (var index = 0; index < second.Length; index++) {
            if (!double.IsFinite(first[index]) || !double.IsFinite(second[index])) {
                return false;
            }
        }

        return true;
    }

    private static double Dot(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right) {
        var result = 0.0;
        for (var index = 0; index < left.Length; index++) {
            result += left[index] * right[index];
        }

        return result;
    }

    private static double InfinityNorm(ReadOnlySpan<double> values) {
        var result = 0.0;
        for (var index = 0; index < values.Length; index++) {
            result = Math.Max(result, Math.Abs(values[index]));
        }

        return result;
    }

    private static MultivariateOptimizationResult FailedMultivariate(
        int iterations,
        int functionEvaluations) {
        return new MultivariateOptimizationResult(
            double.NaN,
            iterations,
            functionEvaluations,
            false);
    }
}
