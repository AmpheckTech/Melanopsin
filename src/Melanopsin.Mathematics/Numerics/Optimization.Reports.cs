// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>The termination status of an optimization attempt.</summary>
public enum OptimizationStatus {
    /// <summary>A finite point satisfying the requested convergence test was produced.</summary>
    Succeeded,

    /// <summary>The iteration budget was exhausted before convergence.</summary>
    MaximumIterationsReached,

    /// <summary>The objective callback returned a non-finite value or rejected its input.</summary>
    FunctionEvaluationFailed,

    /// <summary>The objective gradient callback returned a non-finite value or rejected its input.</summary>
    GradientEvaluationFailed,

    /// <summary>The residual callback returned a non-finite vector or rejected its input.</summary>
    ResidualEvaluationFailed,

    /// <summary>The Jacobian callback returned a non-finite matrix or rejected its input.</summary>
    JacobianEvaluationFailed,

    /// <summary>Backtracking could not find a finite descent step.</summary>
    LineSearchFailed,

    /// <summary>The damped or quasi-Newton system had exactly non-positive curvature.</summary>
    NonPositiveCurvature,

    /// <summary>The normal-equation solve failed numerically.</summary>
    NormalEquationSolveFailed,

    /// <summary>An iteration produced a non-finite point, search direction, or objective value.</summary>
    NonFiniteStep,

    /// <summary>A finite input produced a non-finite derived quantity.</summary>
    NumericalFailure,
}

/// <summary>
/// A compact diagnostic report for a one-dimensional, multivariate, or
/// nonlinear least-squares minimization.
/// </summary>
/// <remarks>
/// Argument validation remains a programming-error boundary and throws before
/// an iteration starts. Numerical callbacks are different: a callback that
/// returns <see langword="false"/> or produces a non-finite value is represented
/// by <see cref="Status"/> in the <c>Try</c> overloads. A callback may still
/// throw its own exception, which is not intercepted. Fields that are not
/// meaningful for the selected method, or for which no finite value was
/// established, contain NaN.
/// </remarks>
public readonly record struct OptimizationReport {
    internal OptimizationReport(
        OptimizationMethod method,
        OptimizationStatus status,
        int iterations,
        int functionEvaluations,
        double objectiveValue,
        double gradientNorm,
        double stepSize,
        double damping) {
        Method = method;
        Status = status;
        Iterations = iterations;
        FunctionEvaluations = functionEvaluations;
        ObjectiveValue = objectiveValue;
        GradientNorm = gradientNorm;
        StepSize = stepSize;
        Damping = damping;
    }

    /// <summary>Gets the executed algorithm.</summary>
    public OptimizationMethod Method { get; }

    /// <summary>Gets the termination status.</summary>
    public OptimizationStatus Status { get; }

    /// <summary>Gets whether a finite point satisfying the convergence test was produced.</summary>
    public bool Succeeded => Status == OptimizationStatus.Succeeded;

    /// <summary>Gets the number of completed or attempted iteration steps.</summary>
    public int Iterations { get; }

    /// <summary>Gets the number of objective or residual evaluations.</summary>
    public int FunctionEvaluations { get; }

    /// <summary>
    /// Gets the final objective value, or the residual sum of squares for
    /// Levenberg-Marquardt; NaN when no finite value was established.
    /// </summary>
    public double ObjectiveValue { get; }

    /// <summary>Gets the final gradient infinity norm, or NaN when unavailable.</summary>
    public double GradientNorm { get; }

    /// <summary>Gets the final scalar step or search step, or NaN when unavailable.</summary>
    public double StepSize { get; }

    /// <summary>Gets the final Levenberg-Marquardt damping value, or NaN for other methods.</summary>
    public double Damping { get; }
}
