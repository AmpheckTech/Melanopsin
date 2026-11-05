// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>Identifies an implemented numerical differentiation method.</summary>
public enum NumericalDifferentiationMethod {
    /// <summary>First-order one-sided forward difference.</summary>
    ForwardDifference,

    /// <summary>First-order one-sided backward difference.</summary>
    BackwardDifference,

    /// <summary>Second-order central first derivative.</summary>
    CentralDifference,

    /// <summary>Fourth-order central first derivative.</summary>
    FourthOrderCentralDifference,

    /// <summary>Second-order central second derivative.</summary>
    CentralSecondDifference,

    /// <summary>Central-difference gradient of a scalar function of a vector.</summary>
    CentralGradient,

    /// <summary>Central-difference Hessian of a scalar function of a vector.</summary>
    CentralHessian,
}

/// <summary>Explicit differentiation properties supplied by the caller.</summary>
[Flags]
public enum NumericalDifferentiationRequirements {
    /// <summary>No special requirement is asserted.</summary>
    None = 0,

    /// <summary>
    /// A forward one-sided evaluation is required, for example at a lower
    /// domain boundary.
    /// </summary>
    OneSided = 1,

    /// <summary>
    /// A backward one-sided evaluation is required, for example at an upper
    /// domain boundary.
    /// </summary>
    BackwardOneSided = 32,

    /// <summary>A fourth-order first derivative is required.</summary>
    FourthOrderAccuracy = 2,

    /// <summary>The second derivative is required.</summary>
    SecondDerivative = 4,

    /// <summary>A gradient is required.</summary>
    Gradient = 8,

    /// <summary>A Hessian is required.</summary>
    Hessian = 16,
}

/// <summary>Explains why a differentiation method was selected.</summary>
public enum NumericalDifferentiationSelectionReason {
    /// <summary>A Hessian selected the central cross-difference formula.</summary>
    Hessian,

    /// <summary>A gradient selected coordinate-wise central differences.</summary>
    Gradient,

    /// <summary>A second derivative selected a central second difference.</summary>
    SecondDerivative,

    /// <summary>A lower-boundary derivative selected a forward difference.</summary>
    OneSidedBoundary,

    /// <summary>An upper-boundary derivative selected a backward difference.</summary>
    BackwardOneSidedBoundary,

    /// <summary>A fourth-order first derivative selected the five-point formula.</summary>
    FourthOrderAccuracy,

    /// <summary>The default first derivative selected the three-point central formula.</summary>
    CentralFirstDerivative,
}

/// <summary>Describes one numerical differentiation method.</summary>
public readonly record struct NumericalDifferentiationDescriptor {
    internal NumericalDifferentiationDescriptor(
        NumericalDifferentiationMethod method,
        string name,
        int functionEvaluations,
        int truncationOrder,
        string numericalCharacteristic) {
        Method = method;
        Name = name;
        FunctionEvaluations = functionEvaluations;
        TruncationOrder = truncationOrder;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the method identifier.</summary>
    public NumericalDifferentiationMethod Method { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets the number of scalar function evaluations away from the center.
    /// Gradient and Hessian entries count one coordinate perturbation each.
    /// </summary>
    public int FunctionEvaluations { get; }

    /// <summary>Gets the formal truncation-error order.</summary>
    public int TruncationOrder { get; }

    /// <summary>Gets the principal accuracy and cancellation trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>The termination status of a numerical differentiation attempt.</summary>
public enum NumericalDifferentiationStatus {
    /// <summary>A finite derivative was produced.</summary>
    Succeeded,

    /// <summary>A required input coordinate was not finite.</summary>
    NonFiniteInput,

    /// <summary>A required function value was not finite.</summary>
    NonFiniteFunctionValue,

    /// <summary>The finite-difference combination was not finite.</summary>
    NonFiniteDerivative,
}

/// <summary>Diagnostic report for a scalar numerical derivative.</summary>
public readonly record struct NumericalDifferentiationReport {
    internal NumericalDifferentiationReport(
        NumericalDifferentiationMethod method,
        NumericalDifferentiationStatus status,
        double value,
        double step) {
        Method = method;
        Status = status;
        Value = value;
        Step = step;
    }

    /// <summary>Gets the method that was executed.</summary>
    public NumericalDifferentiationMethod Method { get; }

    /// <summary>Gets the termination status.</summary>
    public NumericalDifferentiationStatus Status { get; }

    /// <summary>Gets whether the derivative is finite and usable.</summary>
    public bool Succeeded =>
        Status == NumericalDifferentiationStatus.Succeeded;

    /// <summary>Gets the derivative, or NaN on failure.</summary>
    public double Value { get; }

    /// <summary>Gets the actual positive step used.</summary>
    public double Step { get; }
}

/// <summary>
/// Diagnostic report for a multivariate numerical derivative.
/// </summary>
public readonly record struct NumericalDifferentiationMultivariateReport {
    internal NumericalDifferentiationMultivariateReport(
        NumericalDifferentiationMethod method,
        NumericalDifferentiationStatus status,
        double relativeStep,
        int functionEvaluationCount) {
        Method = method;
        Status = status;
        RelativeStep = relativeStep;
        FunctionEvaluationCount = functionEvaluationCount;
    }

    /// <summary>Gets the method that was executed.</summary>
    public NumericalDifferentiationMethod Method { get; }

    /// <summary>Gets the termination status.</summary>
    public NumericalDifferentiationStatus Status { get; }

    /// <summary>Gets whether every derivative component is finite and usable.</summary>
    public bool Succeeded => Status == NumericalDifferentiationStatus.Succeeded;

    /// <summary>Gets the positive relative coordinate step supplied by the caller.</summary>
    public double RelativeStep { get; }

    /// <summary>Gets the number of completed callback evaluations.</summary>
    public int FunctionEvaluationCount { get; }
}

/// <summary>
/// An immutable catalog of numerical differentiation methods.
/// </summary>
/// <remarks>
/// Selection uses only requirements declared by the caller. The catalog does
/// not inspect function samples or choose a step based on observed noise.
/// All central formulas balance truncation error against floating-point
/// cancellation; callers can supply an explicit step when they have a
/// scale-aware policy.
/// </remarks>
public static class NumericalDifferentiationCatalog {
    private static readonly NumericalDifferentiationDescriptor[] Entries = [
        new(
            NumericalDifferentiationMethod.CentralDifference,
            "Central first difference",
            functionEvaluations: 2,
            truncationOrder: 2,
            "Balanced one-sided error; default first derivative."),
        new(
            NumericalDifferentiationMethod.FourthOrderCentralDifference,
            "Fourth-order central first difference",
            functionEvaluations: 4,
            truncationOrder: 4,
            "Higher truncation accuracy with greater cancellation and evaluation cost."),
        new(
            NumericalDifferentiationMethod.CentralSecondDifference,
            "Central second difference",
            functionEvaluations: 3,
            truncationOrder: 2,
            "Second derivative with error sensitive to the square of the step."),
        new(
            NumericalDifferentiationMethod.ForwardDifference,
            "Forward difference",
            functionEvaluations: 2,
            truncationOrder: 1,
            "One-sided first derivative for a domain boundary; lower accuracy."),
        new(
            NumericalDifferentiationMethod.BackwardDifference,
            "Backward difference",
            functionEvaluations: 2,
            truncationOrder: 1,
            "One-sided first derivative for a domain boundary; lower accuracy."),
        new(
            NumericalDifferentiationMethod.CentralGradient,
            "Central gradient",
            functionEvaluations: 2,
            truncationOrder: 2,
            "One coordinate pair per gradient component; caller-owned workspace."),
        new(
            NumericalDifferentiationMethod.CentralHessian,
            "Central Hessian",
            functionEvaluations: 4,
            truncationOrder: 2,
            "Symmetric cross differences require quadratic coordinate work."),
    ];

    /// <summary>Gets the built-in differentiation catalog.</summary>
    public static ReadOnlySpan<NumericalDifferentiationDescriptor> Methods => Entries;

    /// <summary>Selects a method from explicit differentiation requirements.</summary>
    /// <exception cref="ArgumentException">
    /// Mutually incompatible derivative requirements were supplied.
    /// </exception>
    public static NumericalDifferentiationMethod Recommend(
        NumericalDifferentiationRequirements requirements,
        out NumericalDifferentiationSelectionReason reason) {
        var derivativeKinds = 0;
        if ((requirements & NumericalDifferentiationRequirements.SecondDerivative) != 0) {
            derivativeKinds++;
        }

        if ((requirements & NumericalDifferentiationRequirements.Gradient) != 0) {
            derivativeKinds++;
        }

        if ((requirements & NumericalDifferentiationRequirements.Hessian) != 0) {
            derivativeKinds++;
        }

        if (derivativeKinds > 1) {
            throw new ArgumentException(
                "Only one of second derivative, gradient, and Hessian can be requested.",
                nameof(requirements));
        }

        if ((requirements & NumericalDifferentiationRequirements.OneSided) != 0 &&
            (requirements & NumericalDifferentiationRequirements.BackwardOneSided) != 0) {
            throw new ArgumentException(
                "Forward and backward one-sided evaluation are mutually exclusive.",
                nameof(requirements));
        }

        if ((requirements & NumericalDifferentiationRequirements.Hessian) != 0) {
            reason = NumericalDifferentiationSelectionReason.Hessian;
            return NumericalDifferentiationMethod.CentralHessian;
        }

        if ((requirements & NumericalDifferentiationRequirements.Gradient) != 0) {
            reason = NumericalDifferentiationSelectionReason.Gradient;
            return NumericalDifferentiationMethod.CentralGradient;
        }

        if ((requirements & NumericalDifferentiationRequirements.SecondDerivative) != 0) {
            reason = NumericalDifferentiationSelectionReason.SecondDerivative;
            return NumericalDifferentiationMethod.CentralSecondDifference;
        }

        if ((requirements & NumericalDifferentiationRequirements.BackwardOneSided) != 0) {
            reason = NumericalDifferentiationSelectionReason.BackwardOneSidedBoundary;
            return NumericalDifferentiationMethod.BackwardDifference;
        }

        if ((requirements & NumericalDifferentiationRequirements.OneSided) != 0) {
            reason = NumericalDifferentiationSelectionReason.OneSidedBoundary;
            return NumericalDifferentiationMethod.ForwardDifference;
        }

        if ((requirements & NumericalDifferentiationRequirements.FourthOrderAccuracy) != 0) {
            reason = NumericalDifferentiationSelectionReason.FourthOrderAccuracy;
            return NumericalDifferentiationMethod.FourthOrderCentralDifference;
        }

        reason = NumericalDifferentiationSelectionReason.CentralFirstDerivative;
        return NumericalDifferentiationMethod.CentralDifference;
    }
}

public static partial class NumericalDifferentiation {
    /// <summary>
    /// Evaluates one explicitly selected scalar derivative and returns a
    /// non-throwing diagnostic result for non-finite function arithmetic.
    /// </summary>
    /// <param name="method">A scalar first- or second-derivative method.</param>
    /// <param name="function">The function to differentiate.</param>
    /// <param name="value">The finite evaluation point.</param>
    /// <param name="step">
    /// The positive finite step. Pass zero to use
    /// <c>1e-5 * max(1, |value|)</c>.
    /// </param>
    /// <remarks>
    /// Non-finite values returned by <paramref name="function"/> are converted
    /// to <see cref="NumericalDifferentiationStatus.NonFiniteFunctionValue"/>.
    /// Exceptions thrown by the callback itself are not intercepted and
    /// propagate to the caller. The callback may therefore signal domain
    /// errors through its own exception contract.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// A multivariate method was supplied, or the point and step are invalid.
    /// </exception>
    public static NumericalDifferentiationReport Differentiate(
        NumericalDifferentiationMethod method,
        Func<double, double> function,
        double value,
        double step = 0) {
        ArgumentNullException.ThrowIfNull(function);
        if (method is NumericalDifferentiationMethod.CentralGradient or
            NumericalDifferentiationMethod.CentralHessian) {
            throw new ArgumentException(
                "The selected method requires vector input.",
                nameof(method));
        }

        ValidateStep(value, step == 0
            ? DefaultRelativeStep * Math.Max(1, Math.Abs(value))
            : step);
        var actualStep = step == 0
            ? DefaultRelativeStep * Math.Max(1, Math.Abs(value))
            : step;
        var center = function(value);
        if (!double.IsFinite(center)) {
            return new NumericalDifferentiationReport(
                method,
                NumericalDifferentiationStatus.NonFiniteFunctionValue,
                double.NaN,
                actualStep);
        }

        var upper = method == NumericalDifferentiationMethod.ForwardDifference ||
            method == NumericalDifferentiationMethod.BackwardDifference
                ? double.NaN
                : function(value + actualStep);
        var lower = method == NumericalDifferentiationMethod.ForwardDifference ||
            method == NumericalDifferentiationMethod.BackwardDifference
                ? double.NaN
                : function(value - actualStep);
        if (method == NumericalDifferentiationMethod.ForwardDifference) {
            upper = function(value + actualStep);
        } else if (method == NumericalDifferentiationMethod.BackwardDifference) {
            lower = function(value - actualStep);
        }

        var valueAtTwiceStep = double.NaN;
        if (method == NumericalDifferentiationMethod.FourthOrderCentralDifference) {
            valueAtTwiceStep = function(value + (2 * actualStep));
            var lowerTwiceStep = function(value - (2 * actualStep));
            if (!double.IsFinite(valueAtTwiceStep) ||
                !double.IsFinite(lowerTwiceStep)) {
                return new NumericalDifferentiationReport(
                    method,
                    NumericalDifferentiationStatus.NonFiniteFunctionValue,
                    double.NaN,
                    actualStep);
            }

            var fourthOrder =
                (-valueAtTwiceStep + (8 * upper) - (8 * lower) +
                    lowerTwiceStep) /
                (12 * actualStep);
            return CreateScalarReport(method, fourthOrder, actualStep);
        }

        if (!double.IsFinite(upper) || !double.IsFinite(lower)) {
            return new NumericalDifferentiationReport(
                method,
                NumericalDifferentiationStatus.NonFiniteFunctionValue,
                double.NaN,
                actualStep);
        }

        var derivative = method switch {
            NumericalDifferentiationMethod.ForwardDifference =>
                (upper - center) / actualStep,
            NumericalDifferentiationMethod.BackwardDifference =>
                (center - lower) / actualStep,
            NumericalDifferentiationMethod.CentralDifference =>
                (upper - lower) / (2 * actualStep),
            NumericalDifferentiationMethod.CentralSecondDifference =>
                (upper - (2 * center) + lower) /
                    (actualStep * actualStep),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
        return CreateScalarReport(method, derivative, actualStep);
    }

    private static NumericalDifferentiationReport CreateScalarReport(
        NumericalDifferentiationMethod method,
        double derivative,
        double step) {
        return new NumericalDifferentiationReport(
            method,
            double.IsFinite(derivative)
                ? NumericalDifferentiationStatus.Succeeded
                : NumericalDifferentiationStatus.NonFiniteDerivative,
            double.IsFinite(derivative) ? derivative : double.NaN,
            step);
    }
}
