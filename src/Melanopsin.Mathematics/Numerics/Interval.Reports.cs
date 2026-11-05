namespace Melanopsin.Mathematics;

/// <summary>Identifies a closed-interval operation that can fail numerically.</summary>
public enum IntervalOperation {
    /// <summary>Intersection of two intervals.</summary>
    Intersection,

    /// <summary>Division by a closed interval.</summary>
    Division,

    /// <summary>Real square root of an interval.</summary>
    SquareRoot,

    /// <summary>Natural logarithm of an interval.</summary>
    Logarithm,
}

/// <summary>The termination status of a closed-interval operation.</summary>
public enum IntervalStatus {
    /// <summary>A finite interval result was produced.</summary>
    Succeeded,

    /// <summary>The two intervals have no common point.</summary>
    EmptyIntersection,

    /// <summary>The divisor interval contains zero.</summary>
    DivisorContainsZero,

    /// <summary>The interval is outside the real domain of the operation.</summary>
    OutsideDomain,

    /// <summary>Finite endpoints produced a non-finite endpoint.</summary>
    NonFiniteOutput,
}

/// <summary>Diagnostic report for a closed-interval operation.</summary>
public readonly record struct IntervalOperationReport {
    internal IntervalOperationReport(
        IntervalOperation operation,
        IntervalStatus status) {
        Operation = operation;
        Status = status;
    }

    /// <summary>Gets the executed operation.</summary>
    public IntervalOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public IntervalStatus Status { get; }

    /// <summary>Gets whether a finite interval was produced.</summary>
    public bool Succeeded => Status == IntervalStatus.Succeeded;
}
