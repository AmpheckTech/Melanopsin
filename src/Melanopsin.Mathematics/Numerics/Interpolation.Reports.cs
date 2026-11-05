namespace Melanopsin.Mathematics;

/// <summary>Identifies an interpolation setup operation.</summary>
public enum InterpolationOperation {
    /// <summary>Construction of barycentric interpolation weights.</summary>
    BarycentricWeights,
}

/// <summary>The termination status of an interpolation setup operation.</summary>
public enum InterpolationStatus {
    /// <summary>Finite interpolation weights were produced.</summary>
    Succeeded,

    /// <summary>An interpolation node is NaN or infinite.</summary>
    NonFiniteInput,

    /// <summary>Finite nodes produced a non-finite weight.</summary>
    NonFiniteOutput,
}

/// <summary>Diagnostic report for an interpolation setup operation.</summary>
public readonly record struct InterpolationReport {
    internal InterpolationReport(
        InterpolationOperation operation,
        InterpolationStatus status) {
        Operation = operation;
        Status = status;
    }

    /// <summary>Gets the executed operation.</summary>
    public InterpolationOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public InterpolationStatus Status { get; }

    /// <summary>Gets whether finite interpolation data was produced.</summary>
    public bool Succeeded => Status == InterpolationStatus.Succeeded;
}
