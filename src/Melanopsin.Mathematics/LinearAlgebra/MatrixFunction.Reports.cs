namespace Melanopsin.Mathematics;

/// <summary>Identifies a real matrix function.</summary>
public enum MatrixFunctionKind {
    /// <summary>The matrix exponential.</summary>
    Exponential,

    /// <summary>The principal matrix square root.</summary>
    PrincipalSquareRoot,

    /// <summary>The real matrix logarithm.</summary>
    RealLogarithm,

    /// <summary>An integer matrix power.</summary>
    IntegerPower,
}

/// <summary>The termination status of a real matrix-function evaluation.</summary>
public enum MatrixFunctionStatus {
    /// <summary>A finite, representable matrix result was produced.</summary>
    Succeeded,

    /// <summary>The source matrix contains NaN or infinity.</summary>
    NonFiniteInput,

    /// <summary>The matrix norm overflowed despite finite input elements.</summary>
    NormOverflow,

    /// <summary>The scaling exponent would exceed the finite-double range.</summary>
    ScalingLimitExceeded,

    /// <summary>A required LU solve encountered an exactly singular factor.</summary>
    SingularIntermediateFactor,

    /// <summary>A negative integer power requires an inverse, but the input is singular.</summary>
    SingularInput,

    /// <summary>The principal square root required by the matrix logarithm was unavailable.</summary>
    SquareRootUnavailable,

    /// <summary>An iteration produced NaN or infinity.</summary>
    NonFiniteIntermediate,

    /// <summary>An iteration exhausted its budget without meeting the residual test.</summary>
    IterationNotConverged,

    /// <summary>An iterative residual stopped decreasing.</summary>
    IterationDiverged,

    /// <summary>A finite input produced a non-finite final matrix.</summary>
    NonFiniteOutput,
}

/// <summary>
/// A compact diagnostic report for a real matrix-function evaluation.
/// </summary>
/// <remarks>
/// Structural argument errors, including invalid order, short buffers, and
/// overlap, throw before evaluation. Non-finite source values are numerical
/// data failure and are reported by the <c>Try</c> overloads. The functions do
/// not use absolute determinant thresholds: a singular intermediate factor is
/// one that the LU solve identifies as exactly singular in working precision.
/// </remarks>
public readonly record struct MatrixFunctionReport {
    internal MatrixFunctionReport(
        MatrixFunctionKind function,
        MatrixFunctionStatus status) {
        Function = function;
        Status = status;
    }

    /// <summary>Gets the requested matrix function.</summary>
    public MatrixFunctionKind Function { get; }

    /// <summary>Gets the termination status.</summary>
    public MatrixFunctionStatus Status { get; }

    /// <summary>Gets whether a finite matrix result was produced.</summary>
    public bool Succeeded => Status == MatrixFunctionStatus.Succeeded;
}
