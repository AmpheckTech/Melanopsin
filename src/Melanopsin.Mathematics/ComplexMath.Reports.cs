namespace Melanopsin.Mathematics;

/// <summary>The termination status of a complex-root operation.</summary>
public enum ComplexRootStatus {
    /// <summary>All requested roots were produced as finite complex values.</summary>
    Succeeded,

    /// <summary>The radicand has a non-finite real or imaginary component.</summary>
    NonFiniteInput,

    /// <summary>Finite input arithmetic produced a non-finite root.</summary>
    NonFiniteOutput,
}

/// <summary>Diagnostic report for the principal roots of a complex value.</summary>
public readonly record struct ComplexRootReport {
    internal ComplexRootReport(int order, ComplexRootStatus status) {
        Order = order;
        Status = status;
    }

    /// <summary>Gets the requested root order.</summary>
    public int Order { get; }

    /// <summary>Gets the termination status.</summary>
    public ComplexRootStatus Status { get; }

    /// <summary>Gets whether all requested roots are finite.</summary>
    public bool Succeeded => Status == ComplexRootStatus.Succeeded;
}
