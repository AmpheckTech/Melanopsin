namespace Melanopsin.Mathematics;

/// <summary>Identifies a fixed-size complex-matrix operation.</summary>
public enum ComplexMatrixOperation {
    /// <summary>Inversion of a two- or three-by-three complex matrix.</summary>
    Inversion,

    /// <summary>Condition-number estimation for a fixed-size complex matrix.</summary>
    ConditionEstimate,
}

/// <summary>The termination status of a fixed-size complex-matrix operation.</summary>
public enum ComplexMatrixOperationStatus {
    /// <summary>The operation produced a finite, representable result.</summary>
    Succeeded,

    /// <summary>An input contains NaN or infinity where a finite value is required.</summary>
    NonFiniteInput,

    /// <summary>The determinant overflowed or otherwise became non-finite.</summary>
    NonFiniteDeterminant,

    /// <summary>The determinant is exactly zero in the working precision.</summary>
    Singular,

    /// <summary>A finite input produced a result outside the finite double range.</summary>
    NonFiniteOutput,
}

/// <summary>
/// A compact diagnostic report for a fixed-size complex-matrix inversion or
/// condition-number estimate.
/// </summary>
/// <remarks>
/// Inversion rejects only a non-finite determinant or a determinant exactly
/// equal to zero in the working precision. Determinant magnitude is
/// scale-dependent and is never compared with an absolute near-singularity
/// threshold. Use <see cref="ComplexMatrix2x2.TryEstimateConditionNumber(out double, out ComplexMatrixOperationReport)"/>
/// or the corresponding three-by-three overload when an application needs an
/// accuracy decision.
/// </remarks>
public readonly record struct ComplexMatrixOperationReport {
    internal ComplexMatrixOperationReport(
        ComplexMatrixOperation operation,
        ComplexMatrixOperationStatus status) {
        Operation = operation;
        Status = status;
    }

    /// <summary>Gets the executed operation.</summary>
    public ComplexMatrixOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public ComplexMatrixOperationStatus Status { get; }

    /// <summary>Gets whether the operation produced a usable result.</summary>
    public bool Succeeded => Status == ComplexMatrixOperationStatus.Succeeded;
}
