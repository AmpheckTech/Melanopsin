namespace Melanopsin.Mathematics;

/// <summary>Identifies an implemented one-dimensional interpolation method.</summary>
public enum InterpolationMethod {
    /// <summary>Piecewise linear interpolation between adjacent knots.</summary>
    Linear,

    /// <summary>Global Lagrange polynomial interpolation.</summary>
    Lagrange,

    /// <summary>Global polynomial interpolation evaluated by Neville's algorithm.</summary>
    Neville,

    /// <summary>Global polynomial interpolation in second barycentric form.</summary>
    Barycentric,

    /// <summary>Piecewise cubic Hermite interpolation with supplied derivatives.</summary>
    CubicHermite,

    /// <summary>Natural or clamped piecewise cubic spline.</summary>
    CubicSpline,

    /// <summary>Piecewise cubic Hermite interpolation preserving local shape.</summary>
    Pchip,
}

/// <summary>Describes constraints a caller places on an interpolant.</summary>
[Flags]
public enum InterpolationRequirements {
    /// <summary>No special structural constraint is asserted.</summary>
    None = 0,

    /// <summary>The interpolant must preserve monotonicity and local extrema.</summary>
    PreserveShape = 1,

    /// <summary>The interpolant must have a continuous second derivative.</summary>
    ContinuousSecondDerivative = 2,

    /// <summary>The interpolant must be one global polynomial through every node.</summary>
    GlobalPolynomial = 4,

    /// <summary>The interpolant will be evaluated repeatedly at many query points.</summary>
    RepeatedEvaluation = 8,

    /// <summary>The caller can supply first derivatives at every knot.</summary>
    CallerSuppliedDerivatives = 16,

    /// <summary>The caller needs an analytic first derivative at query points.</summary>
    AnalyticFirstDerivative = 32,

    /// <summary>Local knot support is preferred over global polynomial coupling.</summary>
    PreferLocalSupport = 64,
}

/// <summary>Describes one interpolation entry in the built-in catalog.</summary>
public readonly record struct InterpolationMethodDescriptor {
    internal InterpolationMethodDescriptor(
        InterpolationMethod method,
        string name,
        bool isPiecewise,
        string continuity,
        bool preservesShape,
        bool isGlobalPolynomial,
        bool supportsAnalyticDerivative,
        string numericalCharacteristic) {
        Method = method;
        Name = name;
        IsPiecewise = isPiecewise;
        Continuity = continuity;
        PreservesShape = preservesShape;
        IsGlobalPolynomial = isGlobalPolynomial;
        SupportsAnalyticDerivative = supportsAnalyticDerivative;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the method identifier.</summary>
    public InterpolationMethod Method { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the interpolant is piecewise or globally coupled.</summary>
    public bool IsPiecewise { get; }

    /// <summary>Gets the documented continuity class.</summary>
    public string Continuity { get; }

    /// <summary>Gets whether the method preserves monotonicity and local extrema.</summary>
    public bool PreservesShape { get; }

    /// <summary>Gets whether the interpolant is one global polynomial.</summary>
    public bool IsGlobalPolynomial { get; }

    /// <summary>Gets whether the method exposes an analytic derivative.</summary>
    public bool SupportsAnalyticDerivative { get; }

    /// <summary>Gets the principal stability, cost, and shape trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>Explains why the interpolation catalog selected a method.</summary>
public enum InterpolationSelectionReason {
    /// <summary>Shape preservation selected PCHIP.</summary>
    ShapePreserving,

    /// <summary>A continuous second derivative selected a cubic spline.</summary>
    SecondDerivativeContinuity,

    /// <summary>Repeated evaluation of a global polynomial selected barycentric form.</summary>
    GlobalPolynomialRepeatedEvaluation,

    /// <summary>A global polynomial selected Neville evaluation.</summary>
    GlobalPolynomial,

    /// <summary>Caller-supplied knot derivatives selected cubic Hermite interpolation.</summary>
    CallerSuppliedDerivatives,

    /// <summary>An analytic derivative selected a cubic spline.</summary>
    AnalyticFirstDerivative,

    /// <summary>Local support and simplicity selected piecewise linear interpolation.</summary>
    LocalLinear,
}

/// <summary>
/// An immutable catalog of the implemented one-dimensional interpolants.
/// </summary>
/// <remarks>
/// <para>
/// The catalog never inspects sample monotonicity or estimates numerical
/// conditioning. It selects from constraints explicitly declared by the
/// caller. Incompatible constraints are rejected instead of silently weakened.
/// </para>
/// <para>
/// PCHIP is selected when shape preservation is required; it is at most
/// <c>C1</c>. Cubic splines are selected for <c>C2</c> smoothness or integrated
/// piecewise cubic behavior. Barycentric form is preferred over Lagrange's
/// direct formula for repeated evaluation of a global polynomial because it has
/// better numerical behavior and an <c>O(n)</c> evaluation cost after an
/// <c>O(n^2)</c> setup. All methods interpolate the supplied nodes exactly in
/// exact arithmetic; extrapolation policy remains method-specific.
/// </para>
/// </remarks>
public static class InterpolationCatalog {
    private static readonly InterpolationMethodDescriptor[] Entries = [
        new(
            InterpolationMethod.Pchip,
            "PCHIP",
            isPiecewise: true,
            continuity: "C1",
            preservesShape: true,
            isGlobalPolynomial: false,
            supportsAnalyticDerivative: true,
            "Shape-preserving piecewise cubic; no overshoot at monotone data."),
        new(
            InterpolationMethod.CubicSpline,
            "Cubic spline",
            isPiecewise: true,
            continuity: "C2",
            preservesShape: false,
            isGlobalPolynomial: false,
            supportsAnalyticDerivative: true,
            "Smooth piecewise cubic with natural or clamped endpoint constraints."),
        new(
            InterpolationMethod.CubicHermite,
            "Cubic Hermite",
            isPiecewise: true,
            continuity: "C1",
            preservesShape: false,
            isGlobalPolynomial: false,
            supportsAnalyticDerivative: true,
            "Local cubic segments controlled directly by caller-supplied knot derivatives."),
        new(
            InterpolationMethod.Barycentric,
            "Barycentric polynomial",
            isPiecewise: false,
            continuity: "C-infinity",
            preservesShape: false,
            isGlobalPolynomial: true,
            supportsAnalyticDerivative: false,
            "O(n^2) setup and O(n) repeated evaluation with improved conditioning."),
        new(
            InterpolationMethod.Neville,
            "Neville polynomial",
            isPiecewise: false,
            continuity: "C-infinity",
            preservesShape: false,
            isGlobalPolynomial: true,
            supportsAnalyticDerivative: false,
            "O(n^2) evaluation with caller-owned workspace and stable recursive construction."),
        new(
            InterpolationMethod.Lagrange,
            "Lagrange polynomial",
            isPiecewise: false,
            continuity: "C-infinity",
            preservesShape: false,
            isGlobalPolynomial: true,
            supportsAnalyticDerivative: false,
            "Direct textbook form; O(n^2) evaluation and higher coefficient sensitivity."),
        new(
            InterpolationMethod.Linear,
            "Piecewise linear",
            isPiecewise: true,
            continuity: "C0",
            preservesShape: false,
            isGlobalPolynomial: false,
            supportsAnalyticDerivative: false,
            "Local O(1) evaluation after interval lookup; discontinuous first derivative."),
    ];

    /// <summary>Gets the built-in interpolation catalog.</summary>
    public static ReadOnlySpan<InterpolationMethodDescriptor> Methods => Entries;

    /// <summary>
    /// Selects a method from explicit interpolant requirements.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The requested constraints are contradictory or unsupported by the
    /// implemented methods.
    /// </exception>
    public static InterpolationMethod Recommend(
        InterpolationRequirements requirements,
        out InterpolationSelectionReason reason) {
        var shape = (requirements & InterpolationRequirements.PreserveShape) != 0;
        var secondDerivative =
            (requirements & InterpolationRequirements.ContinuousSecondDerivative) != 0;
        var globalPolynomial =
            (requirements & InterpolationRequirements.GlobalPolynomial) != 0;
        if (shape && (secondDerivative || globalPolynomial)) {
            throw new ArgumentException(
                "Shape preservation is incompatible with the implemented C2 or global-polynomial methods.",
                nameof(requirements));
        }

        if (shape) {
            reason = InterpolationSelectionReason.ShapePreserving;
            return InterpolationMethod.Pchip;
        }

        if (secondDerivative) {
            reason = InterpolationSelectionReason.SecondDerivativeContinuity;
            return InterpolationMethod.CubicSpline;
        }

        if (globalPolynomial) {
            reason =
                (requirements & InterpolationRequirements.RepeatedEvaluation) != 0
                    ? InterpolationSelectionReason.GlobalPolynomialRepeatedEvaluation
                    : InterpolationSelectionReason.GlobalPolynomial;
            return (requirements & InterpolationRequirements.RepeatedEvaluation) != 0
                ? InterpolationMethod.Barycentric
                : InterpolationMethod.Neville;
        }

        if ((requirements & InterpolationRequirements.CallerSuppliedDerivatives) != 0) {
            reason = InterpolationSelectionReason.CallerSuppliedDerivatives;
            return InterpolationMethod.CubicHermite;
        }

        if ((requirements & InterpolationRequirements.AnalyticFirstDerivative) != 0) {
            reason = InterpolationSelectionReason.AnalyticFirstDerivative;
            return InterpolationMethod.CubicSpline;
        }

        reason = InterpolationSelectionReason.LocalLinear;
        return InterpolationMethod.Linear;
    }
}
