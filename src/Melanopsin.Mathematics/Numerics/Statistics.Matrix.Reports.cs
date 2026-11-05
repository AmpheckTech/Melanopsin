namespace Melanopsin.Mathematics;

/// <summary>The termination status of a Pearson correlation-matrix computation.</summary>
public enum CorrelationMatrixStatus {
    /// <summary>A finite correlation matrix was produced.</summary>
    Succeeded,

    /// <summary>At least one supplied observation is NaN or infinite.</summary>
    NonFiniteInput,

    /// <summary>A variable has exactly zero variance, so its correlation is undefined.</summary>
    DegenerateSample,

    /// <summary>Finite observations produced a non-finite variance or correlation.</summary>
    NonFiniteOutput,
}

/// <summary>Diagnostic report for a Pearson correlation-matrix computation.</summary>
public readonly record struct CorrelationMatrixReport {
    internal CorrelationMatrixReport(
        CorrelationMatrixStatus status,
        int observationCount,
        int variableCount) {
        Status = status;
        ObservationCount = observationCount;
        VariableCount = variableCount;
    }

    /// <summary>Gets the termination status.</summary>
    public CorrelationMatrixStatus Status { get; }

    /// <summary>Gets the number of processed observations.</summary>
    public int ObservationCount { get; }

    /// <summary>Gets the number of variables.</summary>
    public int VariableCount { get; }

    /// <summary>Gets whether a finite correlation matrix was produced.</summary>
    public bool Succeeded => Status == CorrelationMatrixStatus.Succeeded;
}
