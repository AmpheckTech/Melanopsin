// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Evaluates an ordinary differential equation <c>dy/dt = f(t, y)</c>.
/// </summary>
public delegate void OdeDerivative(
    double time,
    ReadOnlySpan<double> state,
    Span<double> derivative);

/// <summary>
/// Allocation-free fixed-step ordinary differential equation integrators.
/// </summary>
/// <remarks>
/// A positive step count permits both forward and backward integration. The
/// destination receives the final state. Work buffers and the destination
/// must not overlap the initial state.
/// </remarks>
public static class OrdinaryDifferentialEquations {
    public static bool TryEuler(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        int stepCount,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace) {
        return TryEuler(
            derivative,
            initialTime,
            finalTime,
            stepCount,
            initialState,
            finalState,
            workspace,
            out _);
    }

    /// <summary>
    /// Advances an ODE system with Euler's method and returns a detailed
    /// termination report.
    /// </summary>
    public static bool TryEuler(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        int stepCount,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace,
        out OdeSolveReport report) {
        Validate(
            derivative,
            initialTime,
            finalTime,
            stepCount,
            nameof(stepCount),
            initialState,
            finalState,
            workspace,
            initialState.Length);
        var stateLength = initialState.Length;
        var derivativeValues = workspace[..stateLength];
        var state = finalState[..stateLength];
        initialState.CopyTo(state);
        if (initialTime == finalTime) {
            report = CreateSuccessReport(
                OdeIntegrationMethod.Euler,
                0,
                0,
                0,
                finalTime);
            return true;
        }

        var step = (finalTime - initialTime) / stepCount;
        if (!double.IsFinite(step) || step == 0) {
            report = CreateFailureReport(
                OdeIntegrationMethod.Euler,
                OdeSolveStatus.StepSizeUnderflow,
                0,
                0,
                0,
                initialTime);
            return false;
        }

        var time = initialTime;
        for (var iteration = 0; iteration < stepCount; iteration++) {
            var nextTime = time + step;
            if (!double.IsFinite(nextTime)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.Euler,
                    OdeSolveStatus.NonFiniteTime,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (nextTime == time) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.Euler,
                    OdeSolveStatus.StepSizeUnderflow,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time,
                state,
                derivativeValues)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.Euler,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            for (var index = 0; index < stateLength; index++) {
                state[index] += step * derivativeValues[index];
                if (!double.IsFinite(state[index])) {
                    report = CreateFailureReport(
                        OdeIntegrationMethod.Euler,
                        OdeSolveStatus.NonFiniteState,
                        iteration,
                        iteration,
                        0,
                        time);
                    return false;
                }
            }

            time = nextTime;
        }

        report = CreateSuccessReport(
            OdeIntegrationMethod.Euler,
            stepCount,
            stepCount,
            0,
            finalTime);
        return true;
    }

    public static bool TryHeun(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        int stepCount,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace) {
        return TryHeun(
            derivative,
            initialTime,
            finalTime,
            stepCount,
            initialState,
            finalState,
            workspace,
            out _);
    }

    /// <summary>
    /// Advances an ODE system with Heun's method and returns a detailed
    /// termination report.
    /// </summary>
    public static bool TryHeun(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        int stepCount,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace,
        out OdeSolveReport report) {
        var stateLength = initialState.Length;
        Validate(
            derivative,
            initialTime,
            finalTime,
            stepCount,
            nameof(stepCount),
            initialState,
            finalState,
            workspace,
            checked(stateLength * 3));
        var firstDerivative = workspace[..stateLength];
        var secondDerivative = workspace.Slice(stateLength, stateLength);
        var stage = workspace.Slice(stateLength * 2, stateLength);
        var state = finalState[..stateLength];
        initialState.CopyTo(state);
        if (initialTime == finalTime) {
            report = CreateSuccessReport(
                OdeIntegrationMethod.Heun,
                0,
                0,
                0,
                finalTime);
            return true;
        }

        var step = (finalTime - initialTime) / stepCount;
        if (!double.IsFinite(step) || step == 0) {
            report = CreateFailureReport(
                OdeIntegrationMethod.Heun,
                OdeSolveStatus.StepSizeUnderflow,
                0,
                0,
                0,
                initialTime);
            return false;
        }

        var time = initialTime;
        for (var iteration = 0; iteration < stepCount; iteration++) {
            var nextTime = time + step;
            if (!double.IsFinite(nextTime)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.Heun,
                    OdeSolveStatus.NonFiniteTime,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (nextTime == time) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.Heun,
                    OdeSolveStatus.StepSizeUnderflow,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time,
                state,
                firstDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.Heun,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            for (var index = 0; index < stateLength; index++) {
                stage[index] = state[index] + (step * firstDerivative[index]);
                if (!double.IsFinite(stage[index])) {
                    report = CreateFailureReport(
                        OdeIntegrationMethod.Heun,
                        OdeSolveStatus.NonFiniteState,
                        iteration,
                        iteration,
                        0,
                        time);
                    return false;
                }
            }

            if (!TryEvaluateDerivative(
                derivative,
                nextTime,
                stage,
                secondDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.Heun,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            for (var index = 0; index < stateLength; index++) {
                state[index] += 0.5 * step *
                    (firstDerivative[index] + secondDerivative[index]);
                if (!double.IsFinite(state[index])) {
                    report = CreateFailureReport(
                        OdeIntegrationMethod.Heun,
                        OdeSolveStatus.NonFiniteState,
                        iteration,
                        iteration,
                        0,
                        time);
                    return false;
                }
            }

            time = nextTime;
        }

        report = CreateSuccessReport(
            OdeIntegrationMethod.Heun,
            stepCount,
            stepCount,
            0,
            finalTime);
        return true;
    }

    /// <summary>
    /// Advances an ODE system using the classical fourth-order Runge-Kutta
    /// method.
    /// </summary>
    public static bool TryRungeKutta4(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        int stepCount,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace) {
        return TryRungeKutta4(
            derivative,
            initialTime,
            finalTime,
            stepCount,
            initialState,
            finalState,
            workspace,
            out _);
    }

    /// <summary>
    /// Advances an ODE system with classical Runge-Kutta 4 and returns a
    /// detailed termination report.
    /// </summary>
    public static bool TryRungeKutta4(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        int stepCount,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace,
        out OdeSolveReport report) {
        var stateLength = initialState.Length;
        Validate(
            derivative,
            initialTime,
            finalTime,
            stepCount,
            nameof(stepCount),
            initialState,
            finalState,
            workspace,
            checked(stateLength * 5));
        var firstDerivative = workspace[..stateLength];
        var secondDerivative = workspace.Slice(stateLength, stateLength);
        var thirdDerivative = workspace.Slice(stateLength * 2, stateLength);
        var fourthDerivative = workspace.Slice(stateLength * 3, stateLength);
        var stage = workspace.Slice(stateLength * 4, stateLength);
        var state = finalState[..stateLength];
        initialState.CopyTo(state);
        if (initialTime == finalTime) {
            report = CreateSuccessReport(
                OdeIntegrationMethod.RungeKutta4,
                0,
                0,
                0,
                finalTime);
            return true;
        }

        var step = (finalTime - initialTime) / stepCount;
        if (!double.IsFinite(step) || step == 0) {
            report = CreateFailureReport(
                OdeIntegrationMethod.RungeKutta4,
                OdeSolveStatus.StepSizeUnderflow,
                0,
                0,
                0,
                initialTime);
            return false;
        }

        var time = initialTime;
        for (var iteration = 0; iteration < stepCount; iteration++) {
            var nextTime = time + step;
            if (!double.IsFinite(nextTime)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.NonFiniteTime,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (nextTime == time) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.StepSizeUnderflow,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time,
                state,
                firstDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryAddScaled(state, firstDerivative, 0.5 * step, stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.NonFiniteState,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time + (0.5 * step),
                stage,
                secondDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryAddScaled(state, secondDerivative, 0.5 * step, stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.NonFiniteState,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time + (0.5 * step),
                stage,
                thirdDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryAddScaled(state, thirdDerivative, step, stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.NonFiniteState,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                nextTime,
                stage,
                fourthDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.RungeKutta4,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    iteration,
                    iteration,
                    0,
                    time);
                return false;
            }

            for (var index = 0; index < stateLength; index++) {
                state[index] += step / 6 *
                    (firstDerivative[index] +
                        (2 * secondDerivative[index]) +
                        (2 * thirdDerivative[index]) +
                        fourthDerivative[index]);
                if (!double.IsFinite(state[index])) {
                    report = CreateFailureReport(
                        OdeIntegrationMethod.RungeKutta4,
                        OdeSolveStatus.NonFiniteState,
                        iteration,
                        iteration,
                        0,
                        time);
                    return false;
                }
            }

            time = nextTime;
        }

        report = CreateSuccessReport(
            OdeIntegrationMethod.RungeKutta4,
            stepCount,
            stepCount,
            0,
            finalTime);
        return true;
    }

    /// <summary>
    /// Advances an ODE system with the adaptive Dormand-Prince embedded
    /// Runge-Kutta pair (RK45).
    /// </summary>
    /// <remarks>
    /// The local error is controlled componentwise by
    /// <c>absoluteTolerance + relativeTolerance * max(|y|, |y5|)</c>.
    /// Either tolerance may be zero, but at least one must be positive.
    /// A zero initial step requests an automatic first-step estimate. The
    /// final state is overwritten only by accepted steps; if the method returns
    /// false, its contents are unspecified. The embedded pair and its
    /// coefficients follow Dormand and Prince (1980),
    /// <see href="https://doi.org/10.1016/0771-050X(80)90013-3">doi:10.1016/0771-050X(80)90013-3</see>.
    /// </remarks>
    public static bool TryDormandPrince(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace,
        out int acceptedStepCount,
        out int rejectedStepCount,
        double absoluteTolerance = 1e-9,
        double relativeTolerance = 1e-9,
        double initialStep = 0,
        int maximumSteps = 100_000) {
        var succeeded = TryDormandPrince(
            derivative,
            initialTime,
            finalTime,
            initialState,
            finalState,
            workspace,
            out var report,
            absoluteTolerance,
            relativeTolerance,
            initialStep,
            maximumSteps);
        acceptedStepCount = report.AcceptedStepCount;
        rejectedStepCount = report.RejectedStepCount;
        return succeeded;
    }

    public static bool TryDormandPrince(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace,
        out OdeSolveReport report,
        double absoluteTolerance = 1e-9,
        double relativeTolerance = 1e-9,
        double initialStep = 0,
        int maximumSteps = 100_000) {
        var stateLength = initialState.Length;
        Validate(
            derivative,
            initialTime,
            finalTime,
            maximumSteps,
            nameof(maximumSteps),
            initialState,
            finalState,
            workspace,
            checked(stateLength * 8));
        if (!double.IsFinite(absoluteTolerance) || absoluteTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(absoluteTolerance));
        }

        if (!double.IsFinite(relativeTolerance) || relativeTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }

        if (absoluteTolerance == 0 && relativeTolerance == 0) {
            throw new ArgumentOutOfRangeException(
                nameof(relativeTolerance),
                "At least one error tolerance must be positive.");
        }

        if (!double.IsFinite(initialStep) || initialStep < 0) {
            throw new ArgumentOutOfRangeException(nameof(initialStep));
        }

        var acceptedStepCount = 0;
        var rejectedStepCount = 0;
        if (initialTime == finalTime) {
            initialState.CopyTo(finalState);
            report = CreateSuccessReport(
                OdeIntegrationMethod.DormandPrince5,
                0,
                0,
                0,
                finalTime);
            return true;
        }

        var firstDerivative = workspace[..stateLength];
        var secondDerivative = workspace.Slice(stateLength, stateLength);
        var thirdDerivative = workspace.Slice(stateLength * 2, stateLength);
        var fourthDerivative = workspace.Slice(stateLength * 3, stateLength);
        var fifthDerivative = workspace.Slice(stateLength * 4, stateLength);
        var sixthDerivative = workspace.Slice(stateLength * 5, stateLength);
        var seventhDerivative = workspace.Slice(stateLength * 6, stateLength);
        var stage = workspace.Slice(stateLength * 7, stateLength);
        var state = finalState[..stateLength];
        initialState.CopyTo(state);
        var direction = finalTime > initialTime ? 1.0 : -1.0;
        var time = initialTime;
        var span = Math.Abs(finalTime - initialTime);
        var step = initialStep;
        if (step == 0) {
            step = span * 1e-3;
            if (!double.IsFinite(step) || step <= 0) {
                step = span;
            }

            if (!double.IsFinite(step) || step <= 0) {
                var endpointScale = Math.Max(
                    Math.Abs(initialTime),
                    Math.Abs(finalTime));
                step = endpointScale * 1e-3;
            }
        }

        step = Math.Min(step, span);
        if (!double.IsFinite(step) || step <= 0) {
            report = CreateFailureReport(
                OdeIntegrationMethod.DormandPrince5,
                OdeSolveStatus.StepSizeUnderflow,
                0,
                0,
                0,
                time);
            return false;
        }

        for (var iteration = 0; iteration < maximumSteps; iteration++) {
            var remaining = Math.Abs(finalTime - time);
            var actualStep = Math.Min(step, remaining);
            var signedStep = direction * actualStep;
            if (remaining == 0) {
                report = CreateSuccessReport(
                    OdeIntegrationMethod.DormandPrince5,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    finalTime);
                return true;
            }

            if (!double.IsFinite(actualStep) || actualStep <= 0) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.StepSizeUnderflow,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            var nextTime = time + signedStep;
            if (!double.IsFinite(nextTime)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.NonFiniteTime,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (nextTime == time && actualStep != remaining) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.StepSizeUnderflow,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(derivative, time, state, firstDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryAddStateCombination(
                state,
                signedStep,
                firstDerivative,
                0.2,
                secondDerivative,
                0,
                thirdDerivative,
                0,
                fourthDerivative,
                0,
                fifthDerivative,
                0,
                sixthDerivative,
                0,
                stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.NonFiniteState,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time + (0.2 * signedStep),
                stage,
                secondDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryAddStateCombination(
                state,
                signedStep,
                firstDerivative,
                3.0 / 40,
                secondDerivative,
                9.0 / 40,
                thirdDerivative,
                0,
                fourthDerivative,
                0,
                fifthDerivative,
                0,
                sixthDerivative,
                0,
                stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.NonFiniteState,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time + (0.3 * signedStep),
                stage,
                thirdDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryAddStateCombination(
                state,
                signedStep,
                firstDerivative,
                44.0 / 45,
                secondDerivative,
                -56.0 / 15,
                thirdDerivative,
                32.0 / 9,
                fourthDerivative,
                0,
                fifthDerivative,
                0,
                sixthDerivative,
                0,
                stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.NonFiniteState,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time + (0.8 * signedStep),
                stage,
                fourthDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryAddStateCombination(
                state,
                signedStep,
                firstDerivative,
                19372.0 / 6561,
                secondDerivative,
                -25360.0 / 2187,
                thirdDerivative,
                64448.0 / 6561,
                fourthDerivative,
                -212.0 / 729,
                fifthDerivative,
                0,
                sixthDerivative,
                0,
                stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.NonFiniteState,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time + (8.0 * signedStep / 9),
                stage,
                fifthDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryAddStateCombination(
                state,
                signedStep,
                firstDerivative,
                9017.0 / 3168,
                secondDerivative,
                -355.0 / 33,
                thirdDerivative,
                46732.0 / 5247,
                fourthDerivative,
                49.0 / 176,
                fifthDerivative,
                -5103.0 / 18656,
                sixthDerivative,
                0,
                stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.NonFiniteState,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time + signedStep,
                stage,
                sixthDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryAddStateCombination(
                state,
                signedStep,
                firstDerivative,
                35.0 / 384,
                secondDerivative,
                0,
                thirdDerivative,
                500.0 / 1113,
                fourthDerivative,
                125.0 / 192,
                fifthDerivative,
                -2187.0 / 6784,
                sixthDerivative,
                11.0 / 84,
                stage)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.NonFiniteState,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            if (!TryEvaluateDerivative(
                derivative,
                time + signedStep,
                stage,
                seventhDerivative)) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.DerivativeEvaluationFailed,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }

            var errorRatio = 0.0;
            for (var index = 0; index < stateLength; index++) {
                var error = actualStep * Math.Abs(
                    ((35.0 / 384) - (5179.0 / 57600)) * firstDerivative[index] +
                    ((500.0 / 1113) - (7571.0 / 16695)) * thirdDerivative[index] +
                    ((125.0 / 192) - (393.0 / 640)) * fourthDerivative[index] +
                    ((-2187.0 / 6784) - (-92097.0 / 339200)) * fifthDerivative[index] +
                    ((11.0 / 84) - (187.0 / 2100)) * sixthDerivative[index] +
                    (-1.0 / 40) * seventhDerivative[index]);
                var scale = absoluteTolerance +
                    (relativeTolerance * Math.Max(
                        Math.Abs(state[index]),
                        Math.Abs(stage[index])));
                if (!double.IsFinite(scale) || scale <= 0) {
                    report = CreateFailureReport(
                        OdeIntegrationMethod.DormandPrince5,
                        OdeSolveStatus.NonFiniteState,
                        acceptedStepCount + rejectedStepCount,
                        acceptedStepCount,
                        rejectedStepCount,
                        time);
                    return false;
                }

                errorRatio = Math.Max(errorRatio, error / scale);
                if (!double.IsFinite(errorRatio)) {
                    report = CreateFailureReport(
                        OdeIntegrationMethod.DormandPrince5,
                        OdeSolveStatus.NonFiniteState,
                        acceptedStepCount + rejectedStepCount,
                        acceptedStepCount,
                        rejectedStepCount,
                        time);
                    return false;
                }
            }

            if (errorRatio <= 1) {
                stage.CopyTo(state);
                time = nextTime;
                if (actualStep == remaining) {
                    time = finalTime;
                }

                acceptedStepCount++;
                if (actualStep == remaining) {
                    report = CreateSuccessReport(
                        OdeIntegrationMethod.DormandPrince5,
                        acceptedStepCount + rejectedStepCount,
                        acceptedStepCount,
                        rejectedStepCount,
                        finalTime);
                    return true;
                }

                var growth = errorRatio == 0
                    ? 5
                    : Math.Clamp(0.9 * Math.Pow(errorRatio, -0.2), 0.2, 5);
                step = actualStep * growth;
            } else {
                rejectedStepCount++;
                var shrink = Math.Clamp(
                    0.9 * Math.Pow(errorRatio, -0.2),
                    0.1,
                    0.5);
                step = actualStep * shrink;
            }

            if (!double.IsFinite(step) || step == 0) {
                report = CreateFailureReport(
                    OdeIntegrationMethod.DormandPrince5,
                    OdeSolveStatus.StepSizeUnderflow,
                    acceptedStepCount + rejectedStepCount,
                    acceptedStepCount,
                    rejectedStepCount,
                    time);
                return false;
            }
        }

        report = CreateFailureReport(
            OdeIntegrationMethod.DormandPrince5,
            OdeSolveStatus.MaximumStepsReached,
            acceptedStepCount + rejectedStepCount,
            acceptedStepCount,
            rejectedStepCount,
            time);
        return false;
    }

    private static bool TryAddStateCombination(
        ReadOnlySpan<double> state,
        double step,
        ReadOnlySpan<double> first,
        double firstScale,
        ReadOnlySpan<double> second,
        double secondScale,
        ReadOnlySpan<double> third,
        double thirdScale,
        ReadOnlySpan<double> fourth,
        double fourthScale,
        ReadOnlySpan<double> fifth,
        double fifthScale,
        ReadOnlySpan<double> sixth,
        double sixthScale,
        Span<double> destination) {
        for (var index = 0; index < state.Length; index++) {
            destination[index] = state[index] + (step * (
                (firstScale * first[index]) +
                (secondScale * second[index]) +
                (thirdScale * third[index]) +
                (fourthScale * fourth[index]) +
                (fifthScale * fifth[index]) +
                (sixthScale * sixth[index])));
            if (!double.IsFinite(destination[index])) {
                return false;
            }
        }

        return true;
    }

    private static bool TryAddScaled(
        ReadOnlySpan<double> state,
        ReadOnlySpan<double> derivative,
        double scale,
        Span<double> destination) {
        for (var index = 0; index < state.Length; index++) {
            destination[index] = state[index] + (scale * derivative[index]);
            if (!double.IsFinite(destination[index])) {
                return false;
            }
        }

        return true;
    }

    private static OdeSolveReport CreateSuccessReport(
        OdeIntegrationMethod method,
        int completedStepCount,
        int acceptedStepCount,
        int rejectedStepCount,
        double finalTime) {
        return new OdeSolveReport(
            method,
            OdeSolveStatus.Succeeded,
            completedStepCount,
            acceptedStepCount,
            rejectedStepCount,
            finalTime);
    }

    private static OdeSolveReport CreateFailureReport(
        OdeIntegrationMethod method,
        OdeSolveStatus status,
        int completedStepCount,
        int acceptedStepCount,
        int rejectedStepCount,
        double finalTime) {
        return new OdeSolveReport(
            method,
            status,
            completedStepCount,
            acceptedStepCount,
            rejectedStepCount,
            finalTime);
    }

    private static bool TryEvaluateDerivative(
        OdeDerivative derivative,
        double time,
        ReadOnlySpan<double> state,
        Span<double> result) {
        result.Clear();
        derivative(time, state, result);
        for (var index = 0; index < result.Length; index++) {
            if (!double.IsFinite(result[index])) {
                return false;
            }
        }

        return true;
    }

    private static void Validate(
        OdeDerivative derivative,
        double initialTime,
        double finalTime,
        int iterationBudget,
        string iterationBudgetParameterName,
        ReadOnlySpan<double> initialState,
        Span<double> finalState,
        Span<double> workspace,
        int requiredWorkspaceLength) {
        ArgumentNullException.ThrowIfNull(derivative);
        if (!double.IsFinite(initialTime)) {
            throw new ArgumentOutOfRangeException(nameof(initialTime));
        }

        if (!double.IsFinite(finalTime)) {
            throw new ArgumentOutOfRangeException(nameof(finalTime));
        }

        if (iterationBudget <= 0) {
            throw new ArgumentOutOfRangeException(
                iterationBudgetParameterName,
                "The iteration budget must be positive.");
        }
        if (initialState.Length == 0) {
            throw new ArgumentException(
                "The initial state must contain at least one value.",
                nameof(initialState));
        }

        for (var index = 0; index < initialState.Length; index++) {
            if (!double.IsFinite(initialState[index])) {
                throw new ArgumentException(
                    "The initial state must contain only finite values.",
                    nameof(initialState));
            }
        }

        if (finalState.Length < initialState.Length) {
            throw new ArgumentException(
                "The final state buffer is too small.",
                nameof(finalState));
        }

        if (workspace.Length < requiredWorkspaceLength) {
            throw new ArgumentException(
                "The workspace is too small.",
                nameof(workspace));
        }

        if (initialState.Overlaps(finalState) ||
            initialState.Overlaps(workspace) ||
            finalState.Overlaps(workspace)) {
            throw new ArgumentException(
                "The initial state, final state, and workspace must not overlap.",
                nameof(workspace));
        }
    }
}
