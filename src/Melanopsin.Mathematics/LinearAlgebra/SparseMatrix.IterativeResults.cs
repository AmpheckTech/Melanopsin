namespace Melanopsin.Mathematics;

/// <summary>The termination status of a sparse iterative solve.</summary>
public enum SparseIterativeStatus {
    /// <summary>The residual criterion was satisfied.</summary>
    Succeeded,

    /// <summary>The iteration budget was exhausted before convergence.</summary>
    MaximumIterationsReached,

    /// <summary>A method requiring a nonzero diagonal found a structural zero.</summary>
    StructuralZeroDiagonal,

    /// <summary>A scalar recurrence broke down before convergence.</summary>
    Breakdown,

    /// <summary>The iteration produced a non-finite residual or iterate.</summary>
    NonFiniteArithmetic,
}

/// <summary>Diagnostic report for one sparse iterative solve.</summary>
public readonly record struct SparseIterativeReport {
    internal SparseIterativeReport(
        SparseIterativeMethod method,
        SparseIterativeSelectionReason selectionReason,
        SparseIterativeStatus status,
        int iterationCount,
        double residualInfinityNorm) {
        Method = method;
        SelectionReason = selectionReason;
        Status = status;
        IterationCount = iterationCount;
        ResidualInfinityNorm = residualInfinityNorm;
    }

    /// <summary>Gets the method that was executed.</summary>
    public SparseIterativeMethod Method { get; }

    /// <summary>Gets the selection or invocation rationale.</summary>
    public SparseIterativeSelectionReason SelectionReason { get; }

    /// <summary>Gets the termination status.</summary>
    public SparseIterativeStatus Status { get; }

    /// <summary>Gets whether the residual criterion was satisfied.</summary>
    public bool Succeeded => Status == SparseIterativeStatus.Succeeded;

    /// <summary>Gets the number of completed iterations or matrix-vector products.</summary>
    public int IterationCount { get; }

    /// <summary>
    /// Gets the final infinity norm <c>||Ax-b||inf</c>, or positive infinity
    /// when no finite residual is available.
    /// </summary>
    public double ResidualInfinityNorm { get; }
}

public sealed partial class SparseMatrix {
    /// <summary>
    /// Solves a square system by an explicitly selected sparse iterative method
    /// and returns a uniform termination report.
    /// </summary>
    /// <param name="method">The method to execute.</param>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="initialGuess">
    /// The finite initial iterate used by Jacobi and Gauss-Seidel. It is
    /// ignored by conjugate gradients, BiCGSTAB, and GMRES, whose low-level
    /// contracts fix the initial iterate to zero.
    /// </param>
    /// <param name="solution">Receives the final finite iterate.</param>
    /// <param name="workspace">
    /// Caller-owned scratch storage. Its required length is documented by the
    /// selected low-level method in <see cref="SparseIterativeSolverCatalog"/>.
    /// </param>
    /// <param name="report">Receives method, selection, status, iterations, and residual.</param>
    /// <param name="absoluteTolerance">The absolute residual component.</param>
    /// <param name="relativeTolerance">
    /// The residual component relative to the infinity norm of the right-hand
    /// side for stationary methods and to the Euclidean norm for Krylov methods.
    /// </param>
    /// <param name="maximumIterations">The iteration or matrix-vector product budget.</param>
    /// <param name="restart">The GMRES restart dimension when GMRES is selected.</param>
    /// <remarks>
    /// The report is populated from the exact low-level termination path.
    /// In particular, a breakdown on the final permitted iteration is not
    /// reclassified as an exhausted iteration budget.
    /// </remarks>
    public bool TrySolveIterative(
        SparseIterativeMethod method,
        ReadOnlySpan<double> rightHandSide,
        ReadOnlySpan<double> initialGuess,
        Span<double> solution,
        Span<double> workspace,
        out SparseIterativeReport report,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000,
        int restart = 30) {
        if (method is not (SparseIterativeMethod.Jacobi or
            SparseIterativeMethod.GaussSeidel or
            SparseIterativeMethod.ConjugateGradient or
            SparseIterativeMethod.BicgStab or
            SparseIterativeMethod.Gmres)) {
            throw new ArgumentOutOfRangeException(nameof(method));
        }

        return TrySolveIterativeCore(
            method,
            SparseIterativeSelectionReason.ExplicitMethod,
            rightHandSide,
            initialGuess,
            solution,
            workspace,
            out report,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations,
            restart);
    }

    /// <summary>
    /// Solves a square system by a catalog-selected sparse iterative method and
    /// returns a uniform termination report.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="initialGuess">
    /// The finite initial iterate used by Jacobi and Gauss-Seidel. It is
    /// ignored by conjugate gradients, BiCGSTAB, and GMRES, whose low-level
    /// contracts fix the initial iterate to zero.
    /// </param>
    /// <param name="solution">Receives the final finite iterate.</param>
    /// <param name="workspace">
    /// Caller-owned scratch storage. Its required length is documented by the
    /// selected low-level method in <see cref="SparseIterativeSolverCatalog"/>.
    /// </param>
    /// <param name="problemKind">
    /// A mathematical problem property explicitly established by the caller.
    /// The method is selected only from this value.
    /// </param>
    /// <param name="report">Receives method, selection, status, iterations, and residual.</param>
    /// <param name="absoluteTolerance">The absolute residual component.</param>
    /// <param name="relativeTolerance">
    /// The residual component relative to the infinity norm of the right-hand
    /// side for stationary methods and to the Euclidean norm for Krylov methods.
    /// </param>
    /// <param name="maximumIterations">The iteration or matrix-vector product budget.</param>
    /// <param name="restart">The GMRES restart dimension when GMRES is selected.</param>
    /// <remarks>
    /// The report is populated from the exact low-level termination path.
    /// A finite but non-converged early stop is reported as
    /// <see cref="SparseIterativeStatus.Breakdown"/>; non-finite arithmetic and
    /// structural zero diagonals are reported by their dedicated statuses.
    /// </remarks>
    public bool TrySolveIterative(
        ReadOnlySpan<double> rightHandSide,
        ReadOnlySpan<double> initialGuess,
        Span<double> solution,
        Span<double> workspace,
        SparseIterativeProblemKind problemKind,
        out SparseIterativeReport report,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000,
        int restart = 30) {
        var method = SparseIterativeSolverCatalog.Recommend(
            problemKind,
            out var selectionReason);
        return TrySolveIterativeCore(
            method,
            selectionReason,
            rightHandSide,
            initialGuess,
            solution,
            workspace,
            out report,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations,
            restart);
    }

    private bool TrySolveIterativeCore(
        SparseIterativeMethod method,
        SparseIterativeSelectionReason selectionReason,
        ReadOnlySpan<double> rightHandSide,
        ReadOnlySpan<double> initialGuess,
        Span<double> solution,
        Span<double> workspace,
        out SparseIterativeReport report,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations,
        int restart) {
        var usesInitialGuess = method is SparseIterativeMethod.Jacobi or
            SparseIterativeMethod.GaussSeidel;
        if (usesInitialGuess) {
            ValidateStationaryIterationArguments(
                rightHandSide,
                solution,
                absoluteTolerance,
                relativeTolerance,
                maximumIterations);
            if (initialGuess.Length < RowCount) {
                throw new ArgumentException(
                    "The initial guess is too small for a stationary iteration.",
                    nameof(initialGuess));
            }

            if (initialGuess.Overlaps(solution) ||
                initialGuess.Overlaps(workspace) ||
                (method == SparseIterativeMethod.Jacobi &&
                    solution.Overlaps(workspace))) {
                throw new ArgumentException(
                    "The initial guess and output buffers must not overlap.",
                    nameof(initialGuess));
            }

            ValidateFiniteVector(initialGuess, RowCount, nameof(initialGuess));
            if (method == SparseIterativeMethod.GaussSeidel) {
                initialGuess[..RowCount].CopyTo(solution);
            }
        }

        var iterationCount = 0;
        var status = SparseIterativeStatus.Breakdown;
        var succeeded = method switch {
            SparseIterativeMethod.Jacobi => TryJacobiCore(
                rightHandSide,
                initialGuess,
                solution,
                workspace,
                out iterationCount,
                out status,
                absoluteTolerance,
                relativeTolerance,
                maximumIterations),
            SparseIterativeMethod.GaussSeidel => TryGaussSeidelCore(
                rightHandSide,
                solution,
                out iterationCount,
                out status,
                absoluteTolerance,
                relativeTolerance,
                maximumIterations),
            SparseIterativeMethod.ConjugateGradient =>
                TryConjugateGradientCore(
                    rightHandSide,
                    solution,
                    workspace,
                    out iterationCount,
                    out status,
                    absoluteTolerance,
                    relativeTolerance,
                    maximumIterations),
            SparseIterativeMethod.BicgStab =>
                TryBiConjugateGradientStabilizedCore(
                    rightHandSide,
                    solution,
                    workspace,
                    out iterationCount,
                    out status,
                    absoluteTolerance,
                    relativeTolerance,
                    maximumIterations),
            SparseIterativeMethod.Gmres => TryGmresCore(
                rightHandSide,
                solution,
                workspace,
                out iterationCount,
                out status,
                restart,
                absoluteTolerance,
                relativeTolerance,
                maximumIterations),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

        var residual = status == SparseIterativeStatus.StructuralZeroDiagonal
            ? double.PositiveInfinity
            : InfinityResidual(solution, rightHandSide);
        report = new SparseIterativeReport(
            method,
            selectionReason,
            status,
            iterationCount,
            double.IsFinite(residual) ? residual : double.PositiveInfinity);
        return succeeded && status == SparseIterativeStatus.Succeeded;
    }
}
