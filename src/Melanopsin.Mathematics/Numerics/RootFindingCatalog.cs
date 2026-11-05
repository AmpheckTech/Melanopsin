// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>Identifies a one-dimensional root-finding method.</summary>
public enum RootFindingMethod {
    /// <summary>Bisection on a sign-changing bracket.</summary>
    Bisection,

    /// <summary>Newton iteration using an analytic or supplied derivative.</summary>
    Newton,

    /// <summary>Secant iteration without a derivative.</summary>
    Secant,

    /// <summary>Brent's bracket-based method.</summary>
    Brent,
}

/// <summary>Explains why a root-finding method was recommended.</summary>
public enum RootFindingSelectionReason {
    /// <summary>A bracket and derivative are both available.</summary>
    BracketAndDerivative,

    /// <summary>Only a sign-changing bracket is available.</summary>
    BracketOnly,

    /// <summary>Only a derivative is available.</summary>
    DerivativeOnly,

    /// <summary>Neither a reliable bracket nor a derivative is available.</summary>
    DerivativeFree,
}

/// <summary>Describes one root-finding method.</summary>
public readonly record struct RootFindingDescriptor {
    internal RootFindingDescriptor(
        RootFindingMethod method,
        string name,
        bool requiresBracket,
        bool requiresDerivative,
        string numericalCharacteristic) {
        Method = method;
        Name = name;
        RequiresBracket = requiresBracket;
        RequiresDerivative = requiresDerivative;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the method identifier.</summary>
    public RootFindingMethod Method { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets whether a sign-changing bracket is required.</summary>
    public bool RequiresBracket { get; }

    /// <summary>Gets whether a derivative is required.</summary>
    public bool RequiresDerivative { get; }

    /// <summary>Gets the principal convergence and robustness trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>
/// A deterministic catalog for selecting among the existing root-finding
/// implementations.
/// </summary>
/// <remarks>
/// Selection uses only capabilities explicitly reported by the caller. It
/// never probes the function to infer a bracket or derivative, and it does not
/// replace the convergence, residual, and failure result returned by the
/// selected solver.
/// </remarks>
public static class RootFindingCatalog {
    private static readonly RootFindingDescriptor[] Entries = [
        new(
            RootFindingMethod.Brent,
            "Brent",
            requiresBracket: true,
            requiresDerivative: false,
            "Superlinear bracket method with maintained sign change and deterministic fallback."),
        new(
            RootFindingMethod.Bisection,
            "Bisection",
            requiresBracket: true,
            requiresDerivative: false,
            "Linear convergence with the strongest bracket guarantee."),
        new(
            RootFindingMethod.Newton,
            "Newton",
            requiresBracket: false,
            requiresDerivative: true,
            "Quadratic local convergence; sensitive to the initial guess and zero derivative."),
        new(
            RootFindingMethod.Secant,
            "Secant",
            requiresBracket: false,
            requiresDerivative: false,
            "Superlinear derivative-free convergence without a bracket guarantee."),
    ];

    /// <summary>Gets the built-in root-finding catalog.</summary>
    public static ReadOnlySpan<RootFindingDescriptor> Methods => Entries;

    /// <summary>
    /// Recommends a method from explicit capabilities.
    /// </summary>
    public static RootFindingMethod Recommend(
        bool hasSignChangingBracket,
        bool hasDerivative,
        out RootFindingSelectionReason reason) {
        if (hasSignChangingBracket && hasDerivative) {
            reason = RootFindingSelectionReason.BracketAndDerivative;
            return RootFindingMethod.Brent;
        }

        if (hasSignChangingBracket) {
            reason = RootFindingSelectionReason.BracketOnly;
            return RootFindingMethod.Brent;
        }

        if (hasDerivative) {
            reason = RootFindingSelectionReason.DerivativeOnly;
            return RootFindingMethod.Newton;
        }

        reason = RootFindingSelectionReason.DerivativeFree;
        return RootFindingMethod.Secant;
    }
}
