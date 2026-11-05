// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>The termination status of a root-finding attempt.</summary>
public enum RootFindingStatus {
    /// <summary>A finite root estimate satisfying the requested convergence test was produced.</summary>
    Succeeded,

    /// <summary>The iteration budget was exhausted before convergence.</summary>
    MaximumIterationsReached,

    /// <summary>The bracket endpoints do not enclose a sign change.</summary>
    BracketingFailure,

    /// <summary>The function callback returned a non-finite value.</summary>
    FunctionEvaluationFailed,

    /// <summary>The derivative callback returned a non-finite value.</summary>
    DerivativeEvaluationFailed,

    /// <summary>Newton's method encountered an exactly zero derivative.</summary>
    ZeroDerivative,

    /// <summary>The secant denominator was exactly zero.</summary>
    ZeroDenominator,

    /// <summary>An iteration produced a non-finite trial point or step.</summary>
    NonFiniteStep,
}

/// <summary>
/// A compact diagnostic report for an iterative root search.
/// </summary>
/// <remarks>
/// Invalid argument values such as a non-finite tolerance, an empty iteration
/// budget, or an unordered bracket remain programming errors and throw before
/// the iteration starts. Numerical termination is reported through
/// <see cref="Status"/> by the <c>Try</c> methods and by the result returned
/// from the throwing convenience methods.
/// </remarks>
public readonly record struct RootFindReport {
    internal RootFindReport(
        RootFindingMethod method,
        RootFindingStatus status,
        double root,
        double residual,
        int iterations) {
        Method = method;
        Status = status;
        Root = root;
        Residual = residual;
        Iterations = iterations;
    }

    /// <summary>Gets the executed algorithm.</summary>
    public RootFindingMethod Method { get; }

    /// <summary>Gets the termination status.</summary>
    public RootFindingStatus Status { get; }

    /// <summary>Gets whether a converged finite root estimate was produced.</summary>
    public bool Succeeded => Status == RootFindingStatus.Succeeded;

    /// <summary>Gets the root estimate, or NaN when no estimate was accepted.</summary>
    public double Root { get; }

    /// <summary>Gets the final residual, or NaN when no estimate was accepted.</summary>
    public double Residual { get; }

    /// <summary>Gets the number of completed iteration steps or attempted steps.</summary>
    public int Iterations { get; }
}
