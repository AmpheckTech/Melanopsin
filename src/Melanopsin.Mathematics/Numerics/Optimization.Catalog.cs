namespace Melanopsin.Mathematics;

/// <summary>Identifies an implemented scalar or multivariate optimization method.</summary>
public enum OptimizationMethod {
    /// <summary>Golden-section search on a unimodal bracket.</summary>
    GoldenSection,

    /// <summary>Brent minimization on a unimodal bracket.</summary>
    Brent,

    /// <summary>Backtracking gradient descent with finite-difference gradients.</summary>
    GradientDescent,

    /// <summary>BFGS quasi-Newton minimization with a supplied gradient.</summary>
    Bfgs,

    /// <summary>Levenberg-Marquardt nonlinear least-squares minimization.</summary>
    LevenbergMarquardt,
}

/// <summary>Describes the mathematical information available to an optimizer.</summary>
public enum OptimizationProblemKind {
    /// <summary>A local minimum can be enclosed on a finite univariate interval.</summary>
    BracketedUnivariate,

    /// <summary>A smooth multivariate objective is available without an analytic gradient.</summary>
    DerivativeFreeMultivariate,

    /// <summary>A multivariate objective and its analytic gradient are available.</summary>
    AnalyticGradientMultivariate,

    /// <summary>A residual vector and its Jacobian are available.</summary>
    NonlinearLeastSquares,
}

/// <summary>Describes one optimization entry in the built-in catalog.</summary>
public readonly record struct OptimizationMethodDescriptor {
    internal OptimizationMethodDescriptor(
        OptimizationMethod method,
        string name,
        bool requiresBracket,
        bool usesAnalyticGradient,
        bool usesResidualJacobian,
        string numericalCharacteristic) {
        Method = method;
        Name = name;
        RequiresBracket = requiresBracket;
        UsesAnalyticGradient = usesAnalyticGradient;
        UsesResidualJacobian = usesResidualJacobian;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the method identifier.</summary>
    public OptimizationMethod Method { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets whether callers must supply a finite unimodal bracket.</summary>
    public bool RequiresBracket { get; }

    /// <summary>Gets whether the method consumes a caller-supplied objective gradient.</summary>
    public bool UsesAnalyticGradient { get; }

    /// <summary>Gets whether the method consumes a residual vector and Jacobian.</summary>
    public bool UsesResidualJacobian { get; }

    /// <summary>Gets the principal convergence and robustness trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>Explains why the optimization catalog selected a method.</summary>
public enum OptimizationSelectionReason {
    /// <summary>A bracketed unimodal problem favors Brent's method.</summary>
    BracketedUnivariate,

    /// <summary>No analytic gradient favors finite-difference gradient descent.</summary>
    DerivativeFreeMultivariate,

    /// <summary>An analytic gradient favors BFGS.</summary>
    AnalyticGradientMultivariate,

    /// <summary>A residual-and-Jacobian problem favors Levenberg-Marquardt.</summary>
    NonlinearLeastSquares,
}

/// <summary>
/// An immutable catalog of the implemented optimization methods.
/// </summary>
/// <remarks>
/// <para>
/// Selection depends only on mathematical capabilities explicitly declared by
/// the caller. The catalog never probes an objective, infers derivatives, or
/// converts a failed convergence result into success. Algorithms retain their
/// own tolerance and workspace contracts.
/// </para>
/// <para>
/// Brent's method is the default for a bracketed univariate problem because it
/// combines superlinear convergence with a maintained bracket. BFGS is the
/// default for a supplied analytic gradient. Gradient descent is the fallback
/// when only objective values are available, and Levenberg-Marquardt is used
/// for nonlinear least squares because it exploits the Jacobian structure.
/// </para>
/// </remarks>
public static class OptimizationCatalog {
    private static readonly OptimizationMethodDescriptor[] Entries = [
        new(
            OptimizationMethod.Brent,
            "Brent",
            requiresBracket: true,
            usesAnalyticGradient: false,
            usesResidualJacobian: false,
            "Superlinear bracket method with deterministic golden-section fallback."),
        new(
            OptimizationMethod.GoldenSection,
            "Golden-section search",
            requiresBracket: true,
            usesAnalyticGradient: false,
            usesResidualJacobian: false,
            "Linear convergence with the simplest bracket-reduction guarantee."),
        new(
            OptimizationMethod.GradientDescent,
            "Finite-difference gradient descent",
            requiresBracket: false,
            usesAnalyticGradient: false,
            usesResidualJacobian: false,
            "Low memory per iteration; finite-difference cost and step-size sensitivity."),
        new(
            OptimizationMethod.Bfgs,
            "BFGS",
            requiresBracket: false,
            usesAnalyticGradient: true,
            usesResidualJacobian: false,
            "Superlinear local convergence with an order-squared inverse-Hessian approximation."),
        new(
            OptimizationMethod.LevenbergMarquardt,
            "Levenberg-Marquardt",
            requiresBracket: false,
            usesAnalyticGradient: false,
            usesResidualJacobian: true,
            "Damped least squares for residual minimization with a supplied Jacobian."),
    ];

    /// <summary>Gets the built-in optimization method catalog.</summary>
    public static ReadOnlySpan<OptimizationMethodDescriptor> Methods => Entries;

    /// <summary>
    /// Selects a method from the mathematical information available to the
    /// caller.
    /// </summary>
    public static OptimizationMethod Recommend(
        OptimizationProblemKind problemKind,
        out OptimizationSelectionReason reason) {
        switch (problemKind) {
            case OptimizationProblemKind.BracketedUnivariate:
                reason = OptimizationSelectionReason.BracketedUnivariate;
                return OptimizationMethod.Brent;
            case OptimizationProblemKind.DerivativeFreeMultivariate:
                reason = OptimizationSelectionReason.DerivativeFreeMultivariate;
                return OptimizationMethod.GradientDescent;
            case OptimizationProblemKind.AnalyticGradientMultivariate:
                reason = OptimizationSelectionReason.AnalyticGradientMultivariate;
                return OptimizationMethod.Bfgs;
            case OptimizationProblemKind.NonlinearLeastSquares:
                reason = OptimizationSelectionReason.NonlinearLeastSquares;
                return OptimizationMethod.LevenbergMarquardt;
            default:
                throw new ArgumentOutOfRangeException(nameof(problemKind));
        }
    }
}
