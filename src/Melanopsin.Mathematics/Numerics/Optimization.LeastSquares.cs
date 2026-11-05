namespace Melanopsin.Mathematics;

/// <summary>
/// Evaluates a residual vector into a caller-provided destination.
/// </summary>
/// <returns>
/// <see langword="true"/> when every residual is finite and usable.
/// </returns>
public delegate bool SpanResidualFunction(
    ReadOnlySpan<double> parameters,
    Span<double> residual);

/// <summary>
/// Evaluates a residual Jacobian into row-major storage.
/// </summary>
/// <returns>
/// <see langword="true"/> when every Jacobian entry is finite and usable.
/// </returns>
public delegate bool SpanJacobianFunction(
    ReadOnlySpan<double> parameters,
    Span<double> jacobian);

/// <summary>
/// The outcome of a nonlinear least-squares minimization.
/// </summary>
public readonly record struct LeastSquaresResult {
    public LeastSquaresResult(
        double sumOfSquares,
        int iterations,
        int functionEvaluations,
        double damping,
        bool converged) {
        SumOfSquares = sumOfSquares;
        Iterations = iterations;
        FunctionEvaluations = functionEvaluations;
        Damping = damping;
        Converged = converged;
    }

    /// <summary>
    /// Gets the final residual sum of squares, <c>||r||^2</c>.
    /// </summary>
    public double SumOfSquares { get; }

    public int Iterations { get; }

    public int FunctionEvaluations { get; }

    /// <summary>
    /// Gets the final Levenberg-Marquardt damping value.
    /// </summary>
    public double Damping { get; }

    public bool Converged { get; }
}

public static partial class Optimization {
    private const double InitialLevenbergDamping = 1e-3;
    private const double MinimumLevenbergDamping = 1e-14;
    private const double MaximumLevenbergDamping = 1e16;
    private const double LevenbergDampingDecrease = 0.25;
    private const double LevenbergDampingIncrease = 10;

    /// <summary>
    /// Minimizes a nonlinear least-squares objective with the
    /// Levenberg-Marquardt method.
    /// </summary>
    /// <param name="residualFunction">
    /// Writes exactly <paramref name="residualCount"/> finite residuals.
    /// </param>
    /// <param name="jacobianFunction">
    /// Writes the row-major Jacobian with
    /// <paramref name="residualCount"/> rows and <c>parameters.Length</c> columns.
    /// </param>
    /// <param name="parameters">
    /// Contains the finite initial estimate and receives the final estimate.
    /// </param>
    /// <param name="jacobian">
    /// Receives the final Jacobian and requires
    /// <c>residualCount * parameters.Length</c> values.
    /// </param>
    /// <param name="residualCount">The number of residual equations.</param>
    /// <param name="workspace">
    /// At least <c>2 * residualCount + order * order + 3 * order</c> values,
    /// where <c>order</c> is <c>parameters.Length</c>.
    /// </param>
    /// <param name="result">The final numerical and iteration summary.</param>
    /// <param name="gradientTolerance">
    /// The infinity-norm convergence tolerance for the least-squares gradient
    /// <c>J^T r</c>.
    /// </param>
    /// <param name="maximumIterations">The maximum number of damping trials.</param>
    /// <remarks>
    /// The method solves the damped normal equations
    /// <c>(J^T J + lambda diag(J^T J)) delta = -J^T r</c>. A trial is accepted
    /// only when it strictly reduces the residual sum of squares. Exact
    /// non-positive curvature in the damped factor reports failure; no
    /// absolute near-singularity cutoff is applied. The supplied buffers must
    /// not overlap. References: Levenberg (1944),
    /// <see href="https://doi.org/10.1090/qam/10666">doi:10.1090/qam/10666</see>,
    /// and Marquardt (1963),
    /// <see href="https://doi.org/10.1137/0111030">doi:10.1137/0111030</see>.
    /// </remarks>
    public static bool TryLevenbergMarquardt(
        SpanResidualFunction residualFunction,
        SpanJacobianFunction jacobianFunction,
        Span<double> parameters,
        Span<double> jacobian,
        int residualCount,
        Span<double> workspace,
        out LeastSquaresResult result,
        double gradientTolerance = 1e-8,
        int maximumIterations = 200) {
        return TryLevenbergMarquardt(
            residualFunction,
            jacobianFunction,
            parameters,
            jacobian,
            residualCount,
            workspace,
            out result,
            out _,
            gradientTolerance,
            maximumIterations);
    }

    /// <summary>
    /// Attempts Levenberg-Marquardt minimization and reports the numerical
    /// termination reason.
    /// </summary>
    /// <remarks>
    /// Rejected damping trials are recoverable. If the iteration budget ends
    /// immediately after such a trial, the report preserves its specific cause
    /// instead of collapsing it to a generic iteration-limit status.
    /// </remarks>
    public static bool TryLevenbergMarquardt(
        SpanResidualFunction residualFunction,
        SpanJacobianFunction jacobianFunction,
        Span<double> parameters,
        Span<double> jacobian,
        int residualCount,
        Span<double> workspace,
        out LeastSquaresResult result,
        out OptimizationReport report,
        double gradientTolerance = 1e-8,
        int maximumIterations = 200) {
        ArgumentNullException.ThrowIfNull(residualFunction);
        ArgumentNullException.ThrowIfNull(jacobianFunction);
        var order = parameters.Length;
        if (order == 0) {
            throw new ArgumentException(
                "The parameter vector must not be empty.",
                nameof(parameters));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(residualCount);
        if (residualCount < order) {
            throw new ArgumentOutOfRangeException(
                nameof(residualCount),
                residualCount,
                "The system must have at least as many residuals as parameters.");
        }

        var jacobianCount = checked(residualCount * order);
        var normalCount = checked(order * order);
        var requiredWorkspace = checked(
            (2 * residualCount) + normalCount + (3 * order));
        if (jacobian.Length < jacobianCount) {
            throw new ArgumentException(
                "The Jacobian buffer is too small.",
                nameof(jacobian));
        }

        if (workspace.Length < requiredWorkspace) {
            throw new ArgumentException(
                "The Levenberg-Marquardt workspace is too small.",
                nameof(workspace));
        }

        if (!double.IsFinite(gradientTolerance) || gradientTolerance <= 0) {
            throw new ArgumentOutOfRangeException(nameof(gradientTolerance));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIterations);
        if (parameters.Overlaps(jacobian) ||
            parameters.Overlaps(workspace) ||
            jacobian.Overlaps(workspace)) {
            throw new ArgumentException(
                "The parameters, Jacobian, and workspace must not overlap.",
                nameof(workspace));
        }

        for (var index = 0; index < order; index++) {
            if (!double.IsFinite(parameters[index])) {
                throw new ArgumentException(
                    "The initial parameter vector must contain only finite values.",
                    nameof(parameters));
            }
        }

        var residual = workspace[..residualCount];
        var candidateResidual = workspace.Slice(residualCount, residualCount);
        var normal = workspace.Slice(2 * residualCount, normalCount);
        var gradient = workspace.Slice(2 * residualCount + normalCount, order);
        var step = workspace.Slice(
            2 * residualCount + normalCount + order,
            order);
        var candidate = workspace.Slice(
            2 * residualCount + normalCount + (2 * order),
            order);
        var residualJacobian = jacobian[..jacobianCount];
        var functionEvaluations = 0;
        var damping = InitialLevenbergDamping;
        if (!EvaluateResidual(
            residualFunction,
            parameters,
            residual,
            ref functionEvaluations)) {
            result = FailedLeastSquares(0, functionEvaluations);
            report = CreateLeastSquaresReport(
                OptimizationStatus.ResidualEvaluationFailed,
                0,
                functionEvaluations,
                double.NaN,
                double.NaN,
                damping);
            return false;
        }

        var sumOfSquares = SumOfSquares(residual);
        if (!double.IsFinite(sumOfSquares)) {
            result = FailedLeastSquares(0, functionEvaluations);
            report = CreateLeastSquaresReport(
                OptimizationStatus.NumericalFailure,
                0,
                functionEvaluations,
                sumOfSquares,
                double.NaN,
                damping);
            return false;
        }

        if (!EvaluateJacobian(
            jacobianFunction,
            parameters,
            residualJacobian,
            residualCount,
            order)) {
            result = FailedLeastSquares(0, functionEvaluations);
            report = CreateLeastSquaresReport(
                OptimizationStatus.JacobianEvaluationFailed,
                0,
                functionEvaluations,
                sumOfSquares,
                double.NaN,
                damping);
            return false;
        }

        var gradientNorm = double.NaN;
        var lastTrialStatus = OptimizationStatus.MaximumIterationsReached;
        for (var iteration = 1; iteration <= maximumIterations; iteration++) {
            if (!BuildNormalEquations(
                residualJacobian,
                residual,
                normal,
                gradient,
                residualCount,
                order)) {
                result = FailedLeastSquares(
                    iteration - 1,
                    functionEvaluations);
                report = CreateLeastSquaresReport(
                    OptimizationStatus.NumericalFailure,
                    iteration - 1,
                    functionEvaluations,
                    sumOfSquares,
                    gradientNorm,
                    damping);
                return false;
            }

            gradientNorm = InfinityNorm(gradient);
            if (gradientNorm <= gradientTolerance) {
                result = new LeastSquaresResult(
                    sumOfSquares,
                    iteration - 1,
                    functionEvaluations,
                    damping,
                    true);
                report = CreateLeastSquaresReport(
                    OptimizationStatus.Succeeded,
                    iteration - 1,
                    functionEvaluations,
                    sumOfSquares,
                    gradientNorm,
                    damping);
                return true;
            }

            for (var row = 0; row < order; row++) {
                var diagonalOffset = (row * order) + row;
                var diagonal = normal[diagonalOffset];
                if (!(diagonal > 0) || !double.IsFinite(diagonal)) {
                    result = FailedLeastSquares(
                        iteration - 1,
                        functionEvaluations);
                    report = CreateLeastSquaresReport(
                        OptimizationStatus.NonPositiveCurvature,
                        iteration - 1,
                        functionEvaluations,
                        sumOfSquares,
                        gradientNorm,
                        damping);
                    return false;
                }

                normal[diagonalOffset] = diagonal * (1 + damping);
                step[row] = -gradient[row];
            }

            if (!TryCholeskySolveInPlace(normal, step, order)) {
                lastTrialStatus = OptimizationStatus.NormalEquationSolveFailed;
                damping = Math.Min(
                    MaximumLevenbergDamping,
                    damping * LevenbergDampingIncrease);
                continue;
            }

            var finiteCandidate = true;
            for (var index = 0; index < order; index++) {
                candidate[index] = parameters[index] + step[index];
                if (!double.IsFinite(candidate[index])) {
                    finiteCandidate = false;
                    break;
                }
            }

            if (!finiteCandidate) {
                lastTrialStatus = OptimizationStatus.NonFiniteStep;
                damping = Math.Min(
                    MaximumLevenbergDamping,
                    damping * LevenbergDampingIncrease);
                continue;
            }

            if (!EvaluateResidual(
                residualFunction,
                candidate,
                candidateResidual,
                ref functionEvaluations)) {
                lastTrialStatus = OptimizationStatus.ResidualEvaluationFailed;
                damping = Math.Min(
                    MaximumLevenbergDamping,
                    damping * LevenbergDampingIncrease);
                continue;
            }

            var candidateSumOfSquares = SumOfSquares(candidateResidual);
            if (!double.IsFinite(candidateSumOfSquares)) {
                lastTrialStatus = OptimizationStatus.NumericalFailure;
                damping = Math.Min(
                    MaximumLevenbergDamping,
                    damping * LevenbergDampingIncrease);
                continue;
            }

            if (!(candidateSumOfSquares < sumOfSquares)) {
                lastTrialStatus = OptimizationStatus.LineSearchFailed;
                damping = Math.Min(
                    MaximumLevenbergDamping,
                    damping * LevenbergDampingIncrease);
                continue;
            }

            candidate[..order].CopyTo(parameters);
            candidateResidual[..residualCount].CopyTo(residual);
            sumOfSquares = candidateSumOfSquares;
            damping = Math.Max(
                MinimumLevenbergDamping,
                damping * LevenbergDampingDecrease);
            if (!EvaluateJacobian(
                jacobianFunction,
                parameters,
                residualJacobian,
                residualCount,
                order)) {
                result = FailedLeastSquares(iteration, functionEvaluations);
                report = CreateLeastSquaresReport(
                    OptimizationStatus.JacobianEvaluationFailed,
                    iteration,
                    functionEvaluations,
                    sumOfSquares,
                    gradientNorm,
                    damping);
                return false;
            }

            lastTrialStatus = OptimizationStatus.MaximumIterationsReached;
        }

        result = new LeastSquaresResult(
            sumOfSquares,
            maximumIterations,
            functionEvaluations,
            damping,
            false);
        report = CreateLeastSquaresReport(
            lastTrialStatus,
            maximumIterations,
            functionEvaluations,
            sumOfSquares,
            gradientNorm,
            damping);
        return false;
    }

    private static bool BuildNormalEquations(
        ReadOnlySpan<double> jacobian,
        ReadOnlySpan<double> residual,
        Span<double> normal,
        Span<double> gradient,
        int residualCount,
        int order) {
        normal[..(order * order)].Clear();
        gradient[..order].Clear();
        for (var observation = 0; observation < residualCount; observation++) {
            var rowOffset = observation * order;
            var residualValue = residual[observation];
            for (var row = 0; row < order; row++) {
                var rowValue = jacobian[rowOffset + row];
                gradient[row] += rowValue * residualValue;
                if (rowValue == 0) {
                    continue;
                }

                for (var column = 0; column <= row; column++) {
                    normal[(row * order) + column] +=
                        rowValue * jacobian[rowOffset + column];
                }
            }
        }

        for (var row = 0; row < order; row++) {
            for (var column = 0; column < row; column++) {
                normal[(column * order) + row] = normal[(row * order) + column];
            }
        }

        return IsFinite(normal, order * order) && IsFinite(gradient, order);
    }

    private static bool TryCholeskySolveInPlace(
        Span<double> matrix,
        Span<double> rightHandSide,
        int order) {
        for (var row = 0; row < order; row++) {
            var rowOffset = row * order;
            for (var column = 0; column <= row; column++) {
                var value = matrix[rowOffset + column];
                for (var inner = 0; inner < column; inner++) {
                    value -= matrix[rowOffset + inner] *
                        matrix[(column * order) + inner];
                }

                if (row == column) {
                    if (!(value > 0) || !double.IsFinite(value)) {
                        return false;
                    }

                    matrix[rowOffset + column] = Math.Sqrt(value);
                } else {
                    var diagonal = matrix[(column * order) + column];
                    matrix[rowOffset + column] = value / diagonal;
                }
            }
        }

        for (var row = 0; row < order; row++) {
            var value = rightHandSide[row];
            for (var column = 0; column < row; column++) {
                value -= matrix[(row * order) + column] *
                    rightHandSide[column];
            }

            rightHandSide[row] = value / matrix[(row * order) + row];
        }

        for (var row = order - 1; row >= 0; row--) {
            var value = rightHandSide[row];
            for (var column = row + 1; column < order; column++) {
                value -= matrix[(column * order) + row] *
                    rightHandSide[column];
            }

            rightHandSide[row] = value / matrix[(row * order) + row];
        }

        return IsFinite(rightHandSide, order);
    }

    private static bool EvaluateResidual(
        SpanResidualFunction function,
        ReadOnlySpan<double> parameters,
        Span<double> residual,
        ref int functionEvaluations) {
        functionEvaluations++;
        if (!function(parameters, residual)) {
            return false;
        }

        return IsFinite(residual, residual.Length);
    }

    private static bool EvaluateJacobian(
        SpanJacobianFunction function,
        ReadOnlySpan<double> parameters,
        Span<double> jacobian,
        int residualCount,
        int order) {
        if (!function(parameters, jacobian)) {
            return false;
        }

        return IsFinite(jacobian, residualCount * order);
    }

    private static double SumOfSquares(ReadOnlySpan<double> values) {
        var sum = 0.0;
        for (var index = 0; index < values.Length; index++) {
            sum += values[index] * values[index];
        }

        return sum;
    }

    private static bool IsFinite(ReadOnlySpan<double> values, int count) {
        for (var index = 0; index < count; index++) {
            if (!double.IsFinite(values[index])) {
                return false;
            }
        }

        return true;
    }

    private static OptimizationReport CreateLeastSquaresReport(
        OptimizationStatus status,
        int iterations,
        int functionEvaluations,
        double sumOfSquares,
        double gradientNorm,
        double damping) {
        return new OptimizationReport(
            OptimizationMethod.LevenbergMarquardt,
            status,
            iterations,
            functionEvaluations,
            sumOfSquares,
            gradientNorm,
            double.NaN,
            damping);
    }

    private static LeastSquaresResult FailedLeastSquares(
        int iterations,
        int functionEvaluations) {
        return new LeastSquaresResult(
            double.NaN,
            iterations,
            functionEvaluations,
            double.NaN,
            false);
    }
}
