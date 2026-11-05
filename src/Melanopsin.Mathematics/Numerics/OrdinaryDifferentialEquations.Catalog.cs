// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>The termination status of an ODE integration attempt.</summary>
public enum OdeSolveStatus {
    /// <summary>The requested integration interval was completed.</summary>
    Succeeded,

    /// <summary>
    /// The adaptive attempt budget was exhausted before the final time was
    /// reached.
    /// </summary>
    MaximumStepsReached,

    /// <summary>
    /// The derivative callback returned at least one non-finite component.
    /// </summary>
    DerivativeEvaluationFailed,

    /// <summary>
    /// A stage or state update produced a non-finite value.
    /// </summary>
    NonFiniteState,

    /// <summary>
    /// Step-size control could not produce a finite positive step.
    /// </summary>
    StepSizeUnderflow,

    /// <summary>
    /// A finite step caused the next integration time to overflow or otherwise
    /// become non-finite.
    /// </summary>
    NonFiniteTime,
}

/// <summary>
/// A diagnostic report for an ODE integration attempt.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CompletedStepCount"/> is the number of completed fixed steps for
/// fixed-step methods. For adaptive methods it is always
/// <see cref="AcceptedStepCount"/> plus <see cref="RejectedStepCount"/>.
/// A step that is aborted by a callback or arithmetic failure before its error
/// test is not counted as completed.
/// </para>
/// <para>
/// <see cref="AcceptedStepCount"/> counts adaptive steps that advanced the
/// state. <see cref="RejectedStepCount"/> counts adaptive trial steps that were
/// completed but rejected by the local-error test. Both are zero for fixed-step
/// methods.
/// </para>
/// <para>
/// <see cref="FinalTime"/> is the last time reached by all completed,
/// accepted steps. It is the requested final time when and only when
/// <see cref="Succeeded"/> is <see langword="true"/>. The destination state
/// contains only accepted steps; on failure it must not be interpreted as an
/// approximation at the requested endpoint.
/// </para>
/// </remarks>
public readonly record struct OdeSolveReport {
    internal OdeSolveReport(
        OdeIntegrationMethod method,
        OdeSolveStatus status,
        int completedStepCount,
        int acceptedStepCount,
        int rejectedStepCount,
        double finalTime) {
        Method = method;
        Status = status;
        CompletedStepCount = completedStepCount;
        AcceptedStepCount = acceptedStepCount;
        RejectedStepCount = rejectedStepCount;
        FinalTime = finalTime;
    }

    /// <summary>Gets the method that was executed.</summary>
    public OdeIntegrationMethod Method { get; }

    /// <summary>Gets the termination status.</summary>
    public OdeSolveStatus Status { get; }

    /// <summary>Gets whether the integration completed the requested interval.</summary>
    public bool Succeeded => Status == OdeSolveStatus.Succeeded;

    /// <summary>
    /// Gets the number of completed fixed steps, or the sum of accepted and
    /// rejected adaptive steps.
    /// </summary>
    public int CompletedStepCount { get; }

    /// <summary>
    /// Gets the number of adaptive steps that advanced the state. This value is
    /// zero for fixed-step methods.
    /// </summary>
    public int AcceptedStepCount { get; }

    /// <summary>
    /// Gets the number of adaptive trial steps rejected by local error control.
    /// This value is zero for fixed-step methods.
    /// </summary>
    public int RejectedStepCount { get; }

    /// <summary>
    /// Gets the last time reached by accepted steps, or the initial time when no
    /// step has been accepted. This equals the requested final time only on
    /// success.
    /// </summary>
    public double FinalTime { get; }
}

/// <summary>Identifies an implemented ordinary differential equation integrator.</summary>
public enum OdeIntegrationMethod {
    /// <summary>Explicit Euler method.</summary>
    Euler,

    /// <summary>Explicit trapezoidal Heun method.</summary>
    Heun,

    /// <summary>Classical fourth-order Runge-Kutta method.</summary>
    RungeKutta4,

    /// <summary>Dormand-Prince embedded fifth-order Runge-Kutta pair.</summary>
    DormandPrince5,
}

/// <summary>Describes one ODE integrator in the built-in catalog.</summary>
public readonly record struct OdeMethodDescriptor {
    internal OdeMethodDescriptor(
        OdeIntegrationMethod method,
        string name,
        int order,
        int stageCount,
        bool isAdaptive,
        bool providesEmbeddedErrorEstimate,
        int workspaceValuesPerStateComponent,
        string numericalCharacteristic) {
        Method = method;
        Name = name;
        Order = order;
        StageCount = stageCount;
        IsAdaptive = isAdaptive;
        ProvidesEmbeddedErrorEstimate = providesEmbeddedErrorEstimate;
        WorkspaceValuesPerStateComponent = workspaceValuesPerStateComponent;
        NumericalCharacteristic = numericalCharacteristic;
    }

    /// <summary>Gets the method identifier.</summary>
    public OdeIntegrationMethod Method { get; }

    /// <summary>Gets a short human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets the formal order of the local truncation error.</summary>
    public int Order { get; }

    /// <summary>Gets the number of derivative evaluations per step or attempted step.</summary>
    public int StageCount { get; }

    /// <summary>Gets whether step size is controlled by an embedded error estimate.</summary>
    public bool IsAdaptive { get; }

    /// <summary>Gets whether the method exposes an embedded error estimate.</summary>
    public bool ProvidesEmbeddedErrorEstimate { get; }

    /// <summary>
    /// Gets the minimum workspace values required per state component by the
    /// corresponding existing API.
    /// </summary>
    public int WorkspaceValuesPerStateComponent { get; }

    /// <summary>Gets the principal accuracy and cost trade-off.</summary>
    public string NumericalCharacteristic { get; }
}

/// <summary>Explains why the ODE catalog selected a method.</summary>
public enum OdeMethodSelectionReason {
    /// <summary>Adaptive local error control selected the Dormand-Prince pair.</summary>
    AdaptiveErrorControl,

    /// <summary>A low-cost fixed-step request selected Euler's method.</summary>
    LowestFixedStepCost,

    /// <summary>A fixed-step request favors fourth-order accuracy.</summary>
    BalancedFixedStepAccuracy,
}

/// <summary>
/// An immutable catalog of the implemented explicit ODE integrators.
/// </summary>
/// <remarks>
/// <para>
/// The catalog selects only from methods implemented by this library and does
/// not infer stiffness. None of these explicit methods is an automatic choice
/// for a stiff system; callers should select a dedicated implicit method when
/// stiffness has been established.
/// </para>
/// <para>
/// A positive step count permits forward and backward integration in the
/// fixed-step APIs. Adaptive integration uses
/// <c>absoluteTolerance + relativeTolerance * max(|y|, |yNext|)</c> as its
/// local acceptance scale. The embedded Dormand-Prince pair follows Dormand
/// and Prince (1980),
/// <see href="https://doi.org/10.1016/0771-050X(80)90013-3">doi:10.1016/0771-050X(80)90013-3</see>.
/// </para>
/// </remarks>
public static class OdeMethodCatalog {
    private static readonly OdeMethodDescriptor[] Entries = [
        new(
            OdeIntegrationMethod.DormandPrince5,
            "Dormand-Prince 5(4)",
            order: 5,
            stageCount: 7,
            isAdaptive: true,
            providesEmbeddedErrorEstimate: true,
            workspaceValuesPerStateComponent: 8,
            "Adaptive fifth-order solution with a fourth-order embedded error estimate."),
        new(
            OdeIntegrationMethod.RungeKutta4,
            "Classical Runge-Kutta 4",
            order: 4,
            stageCount: 4,
            isAdaptive: false,
            providesEmbeddedErrorEstimate: false,
            workspaceValuesPerStateComponent: 5,
            "Fixed-step fourth-order accuracy with four derivative evaluations per step."),
        new(
            OdeIntegrationMethod.Heun,
            "Heun",
            order: 2,
            stageCount: 2,
            isAdaptive: false,
            providesEmbeddedErrorEstimate: false,
            workspaceValuesPerStateComponent: 3,
            "Fixed-step second-order predictor-corrector method."),
        new(
            OdeIntegrationMethod.Euler,
            "Explicit Euler",
            order: 1,
            stageCount: 1,
            isAdaptive: false,
            providesEmbeddedErrorEstimate: false,
            workspaceValuesPerStateComponent: 1,
            "Lowest cost per step and first-order global accuracy."),
    ];

    /// <summary>Gets the built-in ODE method catalog.</summary>
    public static ReadOnlySpan<OdeMethodDescriptor> Methods => Entries;

    /// <summary>
    /// Selects an explicit method from an explicit adaptivity and cost policy.
    /// </summary>
    /// <param name="requiresAdaptiveErrorControl">
    /// Whether local error control is required.
    /// </param>
    /// <param name="minimizeDerivativeEvaluations">
    /// Whether fixed-step derivative-evaluation cost dominates the accuracy
    /// requirement. This parameter is ignored when adaptive control is required.
    /// </param>
    /// <param name="reason">Receives the policy that selected the method.</param>
    public static OdeIntegrationMethod Recommend(
        bool requiresAdaptiveErrorControl,
        bool minimizeDerivativeEvaluations,
        out OdeMethodSelectionReason reason) {
        if (requiresAdaptiveErrorControl) {
            reason = OdeMethodSelectionReason.AdaptiveErrorControl;
            return OdeIntegrationMethod.DormandPrince5;
        }

        if (minimizeDerivativeEvaluations) {
            reason = OdeMethodSelectionReason.LowestFixedStepCost;
            return OdeIntegrationMethod.Euler;
        }

        reason = OdeMethodSelectionReason.BalancedFixedStepAccuracy;
        return OdeIntegrationMethod.RungeKutta4;
    }
}
