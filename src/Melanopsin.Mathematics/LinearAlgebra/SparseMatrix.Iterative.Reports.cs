// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public sealed partial class SparseMatrix {
    /// <summary>
    /// Solves a square system with the Jacobi iteration and returns a
    /// termination report.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="initialGuess">The finite initial iterate.</param>
    /// <param name="solution">Receives the final finite iterate.</param>
    /// <param name="workspace">At least <c>order</c> scratch values.</param>
    /// <param name="report">Receives the termination status, iteration count, and residual.</param>
    /// <param name="absoluteTolerance">The absolute residual component.</param>
    /// <param name="relativeTolerance">The residual component relative to the right-hand side.</param>
    /// <param name="maximumIterations">The iteration budget.</param>
    /// <returns><see langword="true"/> only when the residual criterion was satisfied.</returns>
    public bool TryJacobi(
        ReadOnlySpan<double> rightHandSide,
        ReadOnlySpan<double> initialGuess,
        Span<double> solution,
        Span<double> workspace,
        out SparseIterativeReport report,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryJacobiCore(
            rightHandSide,
            initialGuess,
            solution,
            workspace,
            out var iterationCount,
            out var status,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        report = CreateSparseIterativeReport(
            SparseIterativeMethod.Jacobi,
            SparseIterativeSelectionReason.ExplicitMethod,
            status,
            iterationCount,
            rightHandSide,
            solution);
        return succeeded;
    }

    /// <summary>
    /// Solves a square system with the Gauss-Seidel iteration and returns a
    /// termination report.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="solution">
    /// Contains the finite initial iterate on entry and receives the final
    /// finite iterate.
    /// </param>
    /// <param name="report">Receives the termination status, iteration count, and residual.</param>
    /// <param name="absoluteTolerance">The absolute residual component.</param>
    /// <param name="relativeTolerance">The residual component relative to the right-hand side.</param>
    /// <param name="maximumIterations">The iteration budget.</param>
    /// <returns><see langword="true"/> only when the residual criterion was satisfied.</returns>
    public bool TryGaussSeidel(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        out SparseIterativeReport report,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryGaussSeidelCore(
            rightHandSide,
            solution,
            out var iterationCount,
            out var status,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        report = CreateSparseIterativeReport(
            SparseIterativeMethod.GaussSeidel,
            SparseIterativeSelectionReason.ExplicitMethod,
            status,
            iterationCount,
            rightHandSide,
            solution);
        return succeeded;
    }

    /// <summary>
    /// Solves a symmetric positive-definite system with conjugate gradients and
    /// returns a termination report.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="solution">Receives the final finite iterate.</param>
    /// <param name="workspace">At least three vectors of order values.</param>
    /// <param name="report">Receives the termination status, iteration count, and residual.</param>
    /// <param name="absoluteTolerance">The absolute residual component.</param>
    /// <param name="relativeTolerance">The residual component relative to the right-hand side.</param>
    /// <param name="maximumIterations">The iteration budget.</param>
    /// <returns><see langword="true"/> only when the residual criterion was satisfied.</returns>
    /// <remarks>
    /// A finite non-positive curvature is reported as
    /// <see cref="SparseIterativeStatus.Breakdown"/>; it is not treated as a
    /// near-singularity threshold. The algorithm follows Hestenes and Stiefel
    /// (1952), <see href="https://doi.org/10.6028/jres.049.044">doi:10.6028/jres.049.044</see>.
    /// </remarks>
    public bool TryConjugateGradient(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out SparseIterativeReport report,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryConjugateGradientCore(
            rightHandSide,
            solution,
            workspace,
            out var iterationCount,
            out var status,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        report = CreateSparseIterativeReport(
            SparseIterativeMethod.ConjugateGradient,
            SparseIterativeSelectionReason.ExplicitMethod,
            status,
            iterationCount,
            rightHandSide,
            solution);
        return succeeded;
    }

    /// <summary>
    /// Solves a general square system with BiCGSTAB and returns a termination
    /// report.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="solution">Receives the final finite iterate.</param>
    /// <param name="workspace">At least six vectors of order values.</param>
    /// <param name="report">Receives the termination status, iteration count, and residual.</param>
    /// <param name="absoluteTolerance">The absolute residual component.</param>
    /// <param name="relativeTolerance">The residual component relative to the right-hand side.</param>
    /// <param name="maximumIterations">The iteration budget.</param>
    /// <returns><see langword="true"/> only when the residual criterion was satisfied.</returns>
    /// <remarks>
    /// The implementation follows van der Vorst (1992),
    /// <see href="https://doi.org/10.1137/0913035">doi:10.1137/0913035</see>.
    /// </remarks>
    public bool TryBiConjugateGradientStabilized(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out SparseIterativeReport report,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryBiConjugateGradientStabilizedCore(
            rightHandSide,
            solution,
            workspace,
            out var iterationCount,
            out var status,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        report = CreateSparseIterativeReport(
            SparseIterativeMethod.BicgStab,
            SparseIterativeSelectionReason.ExplicitMethod,
            status,
            iterationCount,
            rightHandSide,
            solution);
        return succeeded;
    }

    /// <summary>
    /// Solves a general square system with restarted GMRES and returns a
    /// termination report.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="solution">Receives the final finite iterate.</param>
    /// <param name="workspace">The caller-owned GMRES workspace.</param>
    /// <param name="report">Receives the termination status, iteration count, and residual.</param>
    /// <param name="restart">The maximum Krylov subspace dimension per cycle.</param>
    /// <param name="absoluteTolerance">The absolute residual component.</param>
    /// <param name="relativeTolerance">The residual component relative to the right-hand side.</param>
    /// <param name="maximumIterations">The matrix-vector product budget.</param>
    /// <returns><see langword="true"/> only when the residual criterion was satisfied.</returns>
    public bool TryGmres(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out SparseIterativeReport report,
        int restart = 30,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryGmresCore(
            rightHandSide,
            solution,
            workspace,
            out var iterationCount,
            out var status,
            restart,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        report = CreateSparseIterativeReport(
            SparseIterativeMethod.Gmres,
            SparseIterativeSelectionReason.ExplicitMethod,
            status,
            iterationCount,
            rightHandSide,
            solution);
        return succeeded;
    }

    private SparseIterativeReport CreateSparseIterativeReport(
        SparseIterativeMethod method,
        SparseIterativeSelectionReason selectionReason,
        SparseIterativeStatus status,
        int iterationCount,
        ReadOnlySpan<double> rightHandSide,
        ReadOnlySpan<double> solution) {
        var residual = status == SparseIterativeStatus.StructuralZeroDiagonal
            ? double.PositiveInfinity
            : InfinityResidual(solution, rightHandSide);
        return new SparseIterativeReport(
            method,
            selectionReason,
            status,
            iterationCount,
            double.IsFinite(residual) ? residual : double.PositiveInfinity);
    }
}
