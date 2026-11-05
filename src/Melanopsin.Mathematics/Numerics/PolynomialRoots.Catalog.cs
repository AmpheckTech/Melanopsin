// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>Identifies an implemented polynomial root-finding method.</summary>
public enum PolynomialRootFindingMethod {
    /// <summary>Simultaneous Durand-Kerner iteration for all complex roots.</summary>
    DurandKerner,

    /// <summary>Closed-form quadratic formula.</summary>
    Quadratic,
}

/// <summary>Explicit polynomial root problem information.</summary>
public enum PolynomialRootProblemKind {
    /// <summary>A general real-coefficient polynomial whose complex roots are all requested.</summary>
    GeneralRealCoefficients,

    /// <summary>A real quadratic whose real roots are requested.</summary>
    RealQuadratic,

    /// <summary>A complex quadratic whose two roots are requested.</summary>
    ComplexQuadratic,
}

/// <summary>Explains why a polynomial root method was selected.</summary>
public enum PolynomialRootFindingSelectionReason {
    /// <summary>A general polynomial selected simultaneous iteration.</summary>
    GeneralPolynomial,

    /// <summary>A real quadratic selected the stable real quadratic formula.</summary>
    RealQuadratic,

    /// <summary>A complex quadratic selected the complex quadratic formula.</summary>
    ComplexQuadratic,
}

/// <summary>Describes one polynomial root-finding entry.</summary>
public readonly record struct PolynomialRootFindingDescriptor {
    internal PolynomialRootFindingDescriptor(
        PolynomialRootFindingMethod method,
        string name,
        bool supportsArbitraryDegree,
        bool supportsComplexCoefficients,
        string numericalCharacteristic) {
        Method = method;
        Name = name;
        SupportsArbitraryDegree = supportsArbitraryDegree;
        SupportsComplexCoefficients = supportsComplexCoefficients;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the method identifier.</summary>
    public PolynomialRootFindingMethod Method { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets whether arbitrary positive degree is supported.</summary>
    public bool SupportsArbitraryDegree { get; }

    /// <summary>Gets whether coefficients or roots may be complex.</summary>
    public bool SupportsComplexCoefficients { get; }

    /// <summary>Gets the principal convergence and conditioning trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>The termination status of a polynomial root attempt.</summary>
public enum PolynomialRootFindingStatus {
    /// <summary>All requested roots were produced.</summary>
    Succeeded,

    /// <summary>The polynomial has degree zero and no finite root set.</summary>
    ConstantPolynomial,

    /// <summary>Every coefficient is zero; every complex number is a root.</summary>
    ZeroPolynomial,

    /// <summary>The coefficient span was empty or a required value was not finite.</summary>
    InvalidInput,

    /// <summary>Finite input led to overflow, underflow, or another non-finite intermediate.</summary>
    NumericalFailure,

    /// <summary>The iteration limit was exhausted or the iterate became invalid.</summary>
    NonConvergence,

    /// <summary>The quadratic has no root in the requested number domain.</summary>
    NoRootsInRequestedDomain,
}

/// <summary>Diagnostic report for a polynomial root attempt.</summary>
public readonly record struct PolynomialRootFindingReport {
    internal PolynomialRootFindingReport(
        PolynomialRootFindingMethod method,
        PolynomialRootFindingStatus status,
        int degree,
        int rootCount,
        int iterationCount) {
        Method = method;
        Status = status;
        Degree = degree;
        RootCount = rootCount;
        IterationCount = iterationCount;
    }

    /// <summary>Gets the method that was executed.</summary>
    public PolynomialRootFindingMethod Method { get; }

    /// <summary>Gets the termination status.</summary>
    public PolynomialRootFindingStatus Status { get; }

    /// <summary>Gets whether the root attempt succeeded.</summary>
    public bool Succeeded =>
        Status == PolynomialRootFindingStatus.Succeeded;

    /// <summary>Gets the degree after trimming exactly zero leading coefficients.</summary>
    public int Degree { get; }

    /// <summary>Gets the number of roots written to the destination.</summary>
    public int RootCount { get; }

    /// <summary>Gets the completed Durand-Kerner iterations, or zero for a direct method.</summary>
    public int IterationCount { get; }
}

/// <summary>
/// An immutable catalog of polynomial root-finding methods.
/// </summary>
/// <remarks>
/// The catalog selects from the polynomial degree and coefficient domain
/// explicitly supplied by the caller. It never infers a tolerance from
/// coefficient magnitude; relative convergence and rank-like decisions remain
/// explicit parameters of the low-level method.
/// </remarks>
public static class PolynomialRootFindingCatalog {
    private static readonly PolynomialRootFindingDescriptor[] Entries = [
        new(
            PolynomialRootFindingMethod.DurandKerner,
            "Durand-Kerner",
            supportsArbitraryDegree: true,
            supportsComplexCoefficients: false,
            "Simultaneous all-root iteration for real coefficients; convergence depends on degree and root separation."),
        new(
            PolynomialRootFindingMethod.Quadratic,
            "Quadratic formula",
            supportsArbitraryDegree: false,
            supportsComplexCoefficients: true,
            "Direct stable formula for degree at most two, with explicit domain handling."),
    ];

    /// <summary>Gets the built-in polynomial root-finding catalog.</summary>
    public static ReadOnlySpan<PolynomialRootFindingDescriptor> Methods => Entries;

    /// <summary>Selects a method from an explicit polynomial problem kind.</summary>
    public static PolynomialRootFindingMethod Recommend(
        PolynomialRootProblemKind problemKind,
        out PolynomialRootFindingSelectionReason reason) {
        switch (problemKind) {
            case PolynomialRootProblemKind.GeneralRealCoefficients:
                reason = PolynomialRootFindingSelectionReason.GeneralPolynomial;
                return PolynomialRootFindingMethod.DurandKerner;
            case PolynomialRootProblemKind.RealQuadratic:
                reason = PolynomialRootFindingSelectionReason.RealQuadratic;
                return PolynomialRootFindingMethod.Quadratic;
            case PolynomialRootProblemKind.ComplexQuadratic:
                reason = PolynomialRootFindingSelectionReason.ComplexQuadratic;
                return PolynomialRootFindingMethod.Quadratic;
            default:
                throw new ArgumentOutOfRangeException(nameof(problemKind));
        }
    }
}
