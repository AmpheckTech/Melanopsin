// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>Identifies a one-dimensional quadrature rule.</summary>
public enum IntegrationRule {
    /// <summary>Composite trapezoidal rule.</summary>
    Trapezoid,

    /// <summary>Composite Simpson rule.</summary>
    Simpson,

    /// <summary>Recursively bisected adaptive Simpson rule.</summary>
    AdaptiveSimpson,

    /// <summary>Fixed five-point Gauss-Legendre rule.</summary>
    GaussLegendre5,

    /// <summary>Fixed fifteen-point Gauss-Kronrod rule with embedded error estimate.</summary>
    GaussKronrod15,

    /// <summary>Adaptive Gauss-Kronrod rule.</summary>
    AdaptiveGaussKronrod,

    /// <summary>Romberg extrapolation of the trapezoidal rule.</summary>
    Romberg,
}

/// <summary>Describes one quadrature rule.</summary>
public readonly record struct IntegrationDescriptor {
    internal IntegrationDescriptor(
        IntegrationRule rule,
        string name,
        bool isAdaptive,
        bool providesErrorEstimate,
        string numericalCharacteristic) {
        Rule = rule;
        Name = name;
        IsAdaptive = isAdaptive;
        ProvidesErrorEstimate = providesErrorEstimate;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the rule identifier.</summary>
    public IntegrationRule Rule { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the rule refines intervals adaptively.</summary>
    public bool IsAdaptive { get; }

    /// <summary>Gets whether the public operation returns a local error estimate.</summary>
    public bool ProvidesErrorEstimate { get; }

    /// <summary>Gets the principal accuracy and sampling trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>The termination status of a quadrature invocation.</summary>
public enum QuadratureStatus {
    /// <summary>
    /// A finite estimate was produced and, for an adaptive rule, the requested
    /// tolerance was met.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The adaptive refinement budget was exhausted before every interval met
    /// the requested tolerance. <see cref="QuadratureResult.Estimate"/> still
    /// contains the best available estimate.
    /// </summary>
    MaximumDepthReached,

    /// <summary>
    /// The Romberg extrapolation table was exhausted before the requested
    /// tolerance was met. <see cref="QuadratureResult.Estimate"/> still
    /// contains the best available estimate.
    /// </summary>
    MaximumLevelsReached,

    /// <summary>
    /// The integrand returned NaN or an infinity at a requested sample point.
    /// <see cref="QuadratureResult.Estimate"/> is not a usable integral.
    /// </summary>
    NonFiniteFunctionValue,

    /// <summary>
    /// Finite samples produced a non-finite intermediate or final estimate in
    /// the working precision. <see cref="QuadratureResult.Estimate"/> is not a
    /// usable integral.
    /// </summary>
    NonFiniteEstimate,
}

/// <summary>The result of an explicit quadrature-rule invocation.</summary>
/// <remarks>
/// <para>
/// The result-returning <see cref="NumericalIntegration.Integrate"/> overload
/// reports a non-finite integrand value or non-finite numerical intermediate
/// through <see cref="Status"/> instead of throwing. The lower-level throwing
/// APIs retain their <see cref="ArithmeticException"/> contract for the same
/// conditions. Invalid arguments continue to throw argument exceptions from
/// both families of APIs.
/// </para>
/// <para>
/// For adaptive rules, a refinement limit is reported through
/// <see cref="Status"/> rather than by returning NaN or silently claiming that
/// the tolerance was met. Error estimates are local numerical diagnostics, not
/// rigorous enclosures.
/// </para>
/// </remarks>
public readonly record struct QuadratureResult {
    internal QuadratureResult(
        IntegrationRule rule,
        double estimate,
        double errorEstimate,
        QuadratureStatus status) {
        Rule = rule;
        Estimate = estimate;
        ErrorEstimate = errorEstimate;
        Status = status;
    }

    /// <summary>Gets the selected rule.</summary>
    public IntegrationRule Rule { get; }

    /// <summary>Gets the termination status.</summary>
    public QuadratureStatus Status { get; }

    /// <summary>Gets whether the estimate satisfies the rule's success contract.</summary>
    public bool Succeeded => Status == QuadratureStatus.Succeeded;

    /// <summary>Gets the integral estimate.</summary>
    public double Estimate { get; }

    /// <summary>
    /// Gets the embedded local error estimate, or NaN when the selected rule
    /// does not expose one through this API.
    /// </summary>
    public double ErrorEstimate { get; }

    /// <summary>Gets whether <see cref="ErrorEstimate"/> is available.</summary>
    public bool HasErrorEstimate => double.IsFinite(ErrorEstimate);
}

/// <summary>Catalog and unified execution for one-dimensional quadrature rules.</summary>
public static partial class NumericalIntegration {
    private const double CatalogDefaultTolerance = 1e-10;
    private const int CatalogDefaultIntervals = 1024;
    private const int CatalogDefaultMaximumDepth = 30;
    private const int CatalogDefaultMaximumLevels = 20;

    private static readonly IntegrationDescriptor[] CatalogEntries = [
        new(
            IntegrationRule.Trapezoid,
            "Composite trapezoid",
            isAdaptive: false,
            providesErrorEstimate: false,
            "Second-order rule; simple and predictable for modest smoothness."),
        new(
            IntegrationRule.Simpson,
            "Composite Simpson",
            isAdaptive: false,
            providesErrorEstimate: false,
            "Fourth-order rule for an even interval count."),
        new(
            IntegrationRule.AdaptiveSimpson,
            "Adaptive Simpson",
            isAdaptive: true,
            providesErrorEstimate: false,
            "Fourth-order local refinement with an absolute tolerance."),
        new(
            IntegrationRule.GaussLegendre5,
            "Gauss-Legendre 5-point",
            isAdaptive: false,
            providesErrorEstimate: false,
            "Ten-point-order accuracy for smooth integrands."),
        new(
            IntegrationRule.GaussKronrod15,
            "Gauss-Kronrod 15-point",
            isAdaptive: false,
            providesErrorEstimate: true,
            "Fifteenth-order Kronrod estimate with embedded seven-point Gauss comparison."),
        new(
            IntegrationRule.AdaptiveGaussKronrod,
            "Adaptive Gauss-Kronrod",
            isAdaptive: true,
            providesErrorEstimate: false,
            "High-order local refinement with absolute and relative tolerance."),
        new(
            IntegrationRule.Romberg,
            "Romberg",
            isAdaptive: false,
            providesErrorEstimate: false,
            "Extrapolated trapezoid table for smooth integrands."),
    ];

    /// <summary>Gets the built-in quadrature catalog.</summary>
    public static ReadOnlySpan<IntegrationDescriptor> Catalog => CatalogEntries;

    /// <summary>
    /// Evaluates an explicitly selected quadrature rule.
    /// </summary>
    /// <param name="rule">The rule to execute.</param>
    /// <param name="function">The finite-valued integrand.</param>
    /// <param name="lower">The lower integration bound.</param>
    /// <param name="upper">The upper integration bound.</param>
    /// <param name="absoluteTolerance">
    /// Used by adaptive Simpson and adaptive Gauss-Kronrod; ignored by fixed rules.
    /// </param>
    /// <param name="maximumDepth">
    /// Used by adaptive Simpson and adaptive Gauss-Kronrod; ignored by fixed rules.
    /// </param>
    /// <remarks>
    /// Fixed composite rules use 1024 intervals and Romberg uses 20 levels.
    /// This high-level entry point intentionally chooses no rule automatically.
    /// Select a rule from <see cref="Catalog"/> or another explicit policy.
    /// </remarks>
    public static QuadratureResult Integrate(
        IntegrationRule rule,
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance = CatalogDefaultTolerance,
        int maximumDepth = CatalogDefaultMaximumDepth) {
        ArgumentNullException.ThrowIfNull(function);
        ValidateTolerance(absoluteTolerance);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);
        switch (rule) {
            case IntegrationRule.Trapezoid:
                return CreateFixedResult(
                    rule,
                    TryTrapezoidal(
                        function,
                        lower,
                        upper,
                        CatalogDefaultIntervals,
                        out var trapezoidStatus),
                    trapezoidStatus);
            case IntegrationRule.Simpson:
                return CreateFixedResult(
                    rule,
                    TrySimpson(
                        function,
                        lower,
                        upper,
                        CatalogDefaultIntervals,
                        out var simpsonStatus),
                    simpsonStatus);
            case IntegrationRule.AdaptiveSimpson:
                return CreateAdaptiveSimpsonResult(
                function,
                lower,
                upper,
                absoluteTolerance,
                maximumDepth);
            case IntegrationRule.GaussLegendre5:
                return CreateFixedResult(
                    rule,
                    TryGaussLegendre5(
                        function,
                        lower,
                        upper,
                        out var gaussLegendreStatus),
                    gaussLegendreStatus);
            case IntegrationRule.GaussKronrod15:
                return CreateGaussKronrodResult(function, lower, upper);
            case IntegrationRule.AdaptiveGaussKronrod:
                return CreateAdaptiveGaussKronrodResult(
                    function,
                    lower,
                    upper,
                    absoluteTolerance,
                    maximumDepth);
            case IntegrationRule.Romberg:
                return CreateRombergResult(
                    function,
                    lower,
                    upper,
                    absoluteTolerance);
            default:
                throw new ArgumentOutOfRangeException(nameof(rule));
        }
    }

    private static QuadratureResult CreateFixedResult(
        IntegrationRule rule,
        double estimate,
        QuadratureStatus status) {
        return new QuadratureResult(
            rule,
            estimate,
            double.NaN,
            status);
    }

    private static QuadratureResult CreateAdaptiveSimpsonResult(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        int maximumDepth) {
        var estimate = IntegrateAdaptiveSimpson(
            function,
            lower,
            upper,
            absoluteTolerance,
            maximumDepth,
            out var status);
        return new QuadratureResult(
            IntegrationRule.AdaptiveSimpson,
            estimate,
            double.NaN,
            status);
    }

    private static QuadratureResult CreateGaussKronrodResult(
        Func<double, double> function,
        double lower,
        double upper) {
        var estimate = TryGaussKronrod15(
            function,
            lower,
            upper,
            out var errorEstimate,
            out var status);
        return new QuadratureResult(
            IntegrationRule.GaussKronrod15,
            estimate,
            errorEstimate,
            status);
    }

    private static QuadratureResult CreateAdaptiveGaussKronrodResult(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance,
        int maximumDepth) {
        var estimate = IntegrateAdaptiveGaussKronrod(
            function,
            lower,
            upper,
            absoluteTolerance,
            absoluteTolerance,
            maximumDepth,
            out var status);
        return new QuadratureResult(
            IntegrationRule.AdaptiveGaussKronrod,
            estimate,
            double.NaN,
            status);
    }

    private static QuadratureResult CreateRombergResult(
        Func<double, double> function,
        double lower,
        double upper,
        double absoluteTolerance) {
        _ = TryRombergPooled(
            function,
            lower,
            upper,
            absoluteTolerance,
            CatalogDefaultMaximumLevels,
            out var estimate,
            out var status);
        return new QuadratureResult(
            IntegrationRule.Romberg,
            estimate,
            double.NaN,
            status);
    }
}
