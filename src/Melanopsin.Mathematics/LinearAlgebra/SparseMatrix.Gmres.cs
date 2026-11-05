// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public sealed partial class SparseMatrix {
    /// <summary>
    /// Solves a general square system with restarted GMRES, starting from a
    /// zero initial guess.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="solution">
    /// Receives the final iterate. It must not overlap the right-hand side or
    /// <paramref name="workspace"/>.
    /// </param>
    /// <param name="workspace">
    /// A caller-owned buffer. Its required length is
    /// <c>(order + 1) * order + (restart + 1) * restart + 3 * restart + 1</c>
    /// values.
    /// </param>
    /// <param name="iterationCount">The number of completed matrix-vector products.</param>
    /// <param name="restart">The maximum Krylov subspace dimension per cycle.</param>
    /// <param name="absoluteTolerance">
    /// The absolute part of the true-residual convergence threshold.
    /// </param>
    /// <param name="relativeTolerance">
    /// The scale-relative part of the true-residual convergence threshold.
    /// </param>
    /// <param name="maximumIterations">The total matrix-vector product budget.</param>
    /// <remarks>
    /// The convergence test is
    /// <c>absoluteTolerance + relativeTolerance * ||rightHandSide||</c> applied
    /// to the true residual after every restart. The temporary Arnoldi residual
    /// is used only to decide whether to solve the current cycle early.
    /// No absolute near-singularity threshold is applied. A breakdown or
    /// non-finite arithmetic reports false; the solution then contains the most
    /// recent finite iterate. Restarted GMRES follows Saad and Schultz (1986),
    /// <see href="https://doi.org/10.1137/0907058">doi:10.1137/0907058</see>.
    /// </remarks>
    public bool TryGmres(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out int iterationCount,
        int restart = 30,
        double absoluteTolerance = 1e-10,
        double relativeTolerance = 1e-10,
        int maximumIterations = 10_000) {
        var succeeded = TryGmres(
            rightHandSide,
            solution,
            workspace,
            out SparseIterativeReport report,
            restart,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        iterationCount = report.IterationCount;
        return succeeded;
    }

    /// <summary>
    /// Solves a general square system with restarted GMRES, starting from a
    /// zero initial guess.
    /// </summary>
    /// <param name="rightHandSide">The finite right-hand side vector.</param>
    /// <param name="solution">
    /// Receives the final iterate. It must not overlap the right-hand side or
    /// <paramref name="workspace"/>.
    /// </param>
    /// <param name="workspace">
    /// A caller-owned buffer. Its required length is
    /// <c>(order + 1) * order + (restart + 1) * restart + 3 * restart + 1</c>
    /// values.
    /// </param>
    /// <param name="iterationCount">The number of completed matrix-vector products.</param>
    /// <param name="status">Receives the numerical or iteration termination status.</param>
    /// <param name="restart">The maximum Krylov subspace dimension per cycle.</param>
    /// <param name="absoluteTolerance">
    /// The absolute part of the true-residual convergence threshold.
    /// </param>
    /// <param name="relativeTolerance">
    /// The scale-relative part of the true-residual convergence threshold.
    /// </param>
    /// <param name="maximumIterations">The total matrix-vector product budget.</param>
    /// <remarks>
    /// The convergence test is
    /// <c>absoluteTolerance + relativeTolerance * ||rightHandSide||</c> applied
    /// to the true residual after every restart. The temporary Arnoldi residual
    /// is used only to decide whether to solve the current cycle early.
    /// No absolute near-singularity threshold is applied. A breakdown or
    /// non-finite arithmetic reports false; the solution then contains the most
    /// recent finite iterate. Restarted GMRES follows Saad and Schultz (1986),
    /// <see href="https://doi.org/10.1137/0907058">doi:10.1137/0907058</see>.
    /// </remarks>
    private bool TryGmresCore(
        ReadOnlySpan<double> rightHandSide,
        Span<double> solution,
        Span<double> workspace,
        out int iterationCount,
        out SparseIterativeStatus status,
        int restart,
        double absoluteTolerance,
        double relativeTolerance,
        int maximumIterations) {
        ValidateIterativeSolveArguments(
            rightHandSide,
            solution,
            workspace,
            absoluteTolerance,
            relativeTolerance,
            maximumIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(restart);
        var order = RowCount;
        var basisCount = checked((order + 1) * order);
        var hessenbergCount = checked((restart + 1) * restart);
        var smallCount = checked(restart + 1);
        var requiredLength = checked(
            basisCount + hessenbergCount + (2 * restart) + smallCount);
        if (workspace.Length < requiredLength) {
            throw new ArgumentException(
                "The GMRES workspace is too small.",
                nameof(workspace));
        }

        var basis = workspace[..basisCount];
        var hessenberg = workspace.Slice(basisCount, hessenbergCount);
        var cosine = workspace.Slice(basisCount + hessenbergCount, restart);
        var sine = workspace.Slice(
            basisCount + hessenbergCount + restart,
            restart);
        var transformedResidual = workspace.Slice(
            basisCount + hessenbergCount + (2 * restart),
            smallCount);
        solution[..order].Clear();
        var rightHandSideNorm = EuclideanNorm(rightHandSide, order);
        var threshold = absoluteTolerance +
            (relativeTolerance * rightHandSideNorm);
        if (rightHandSideNorm <= threshold) {
            iterationCount = 0;
            status = SparseIterativeStatus.Succeeded;
            return true;
        }

        var completedIterations = 0;
        var currentResidualNorm = rightHandSideNorm;
        while (completedIterations < maximumIterations &&
            currentResidualNorm > threshold) {
            var cycleIterations = Math.Min(
                restart,
                maximumIterations - completedIterations);
            hessenberg[..checked((cycleIterations + 1) * cycleIterations)].Clear();
            transformedResidual[..(cycleIterations + 1)].Clear();
            rightHandSide[..order].CopyTo(basis[..order]);
            if (completedIterations > 0) {
                var product = basis.Slice(order, order);
                Multiply(solution[..order], product);
                for (var index = 0; index < order; index++) {
                    if (!double.IsFinite(product[index])) {
                        iterationCount = completedIterations;
                        status = SparseIterativeStatus.NonFiniteArithmetic;
                        return false;
                    }

                    basis[index] = rightHandSide[index] - product[index];
                }

                currentResidualNorm = EuclideanNorm(basis, order);
                if (currentResidualNorm <= threshold) {
                    iterationCount = completedIterations;
                    status = SparseIterativeStatus.Succeeded;
                    return true;
                }
            }

            var inverseResidualNorm = 1 / currentResidualNorm;
            for (var index = 0; index < order; index++) {
                basis[index] *= inverseResidualNorm;
            }

            transformedResidual[0] = currentResidualNorm;
            var cycleConverged = false;
            var breakdown = false;
            var solvedIterations = 0;
            for (var inner = 0; inner < cycleIterations; inner++) {
                var current = basis.Slice(inner * order, order);
                var next = basis.Slice((inner + 1) * order, order);
                Multiply(current, next);
                var columnOffset = inner * (cycleIterations + 1);
                for (var orthogonalization = 0;
                    orthogonalization <= inner;
                    orthogonalization++) {
                    var coefficient = Dot(
                        next,
                        basis.Slice(orthogonalization * order, order),
                        order);
                    hessenberg[columnOffset + orthogonalization] = coefficient;
                    AddScaledInPlace(
                        next,
                        basis.Slice(orthogonalization * order, order),
                        -coefficient,
                        order);
                }

                var nextNorm = EuclideanNorm(next, order);
                if (!double.IsFinite(nextNorm)) {
                    iterationCount = completedIterations + inner;
                    status = SparseIterativeStatus.NonFiniteArithmetic;
                    return false;
                }

                hessenberg[columnOffset + inner + 1] = nextNorm;
                if (nextNorm == 0) {
                    breakdown = true;
                } else {
                    var inverseNorm = 1 / nextNorm;
                    for (var index = 0; index < order; index++) {
                        next[index] *= inverseNorm;
                    }
                }

                for (var rotation = 0; rotation < inner; rotation++) {
                    var first = hessenberg[columnOffset + rotation];
                    var second = hessenberg[columnOffset + rotation + 1];
                    hessenberg[columnOffset + rotation] =
                        (cosine[rotation] * first) + (sine[rotation] * second);
                    hessenberg[columnOffset + rotation + 1] =
                        (-sine[rotation] * first) + (cosine[rotation] * second);
                }

                var diagonal = hessenberg[columnOffset + inner];
                var subdiagonal = hessenberg[columnOffset + inner + 1];
                var rotationNorm = ElementaryFunctions.Hypot(diagonal, subdiagonal);
                if (rotationNorm == 0) {
                    breakdown = true;
                    cosine[inner] = 1;
                    sine[inner] = 0;
                } else {
                    cosine[inner] = diagonal / rotationNorm;
                    sine[inner] = subdiagonal / rotationNorm;
                    hessenberg[columnOffset + inner] = rotationNorm;
                    hessenberg[columnOffset + inner + 1] = 0;
                }

                var currentTransformedResidual = transformedResidual[inner];
                transformedResidual[inner] = cosine[inner] *
                    currentTransformedResidual;
                transformedResidual[inner + 1] = -sine[inner] *
                    currentTransformedResidual;
                completedIterations++;
                solvedIterations = inner + 1;
                var estimatedResidual = Math.Abs(transformedResidual[inner + 1]);
                if (estimatedResidual <= threshold) {
                    cycleConverged = true;
                    break;
                }

                if (breakdown) {
                    break;
                }
            }

            if (!TrySolveArnoldiSystem(
                hessenberg,
                cycleIterations + 1,
                transformedResidual,
                solvedIterations)) {
                iterationCount = completedIterations;
                status = SparseIterativeStatus.Breakdown;
                return false;
            }

            for (var k = 0; k < solvedIterations; k++) {
                var coefficient = transformedResidual[k];
                AddScaledInPlace(
                    solution[..order],
                    basis.Slice(k * order, order),
                    coefficient,
                    order);
            }

            if (cycleConverged) {
                var product = basis.Slice(order, order);
                Multiply(solution[..order], product);
                var trueResidualSquared = 0.0;
                for (var index = 0; index < order; index++) {
                    var residual = rightHandSide[index] - product[index];
                    trueResidualSquared += residual * residual;
                }

                currentResidualNorm = Math.Sqrt(trueResidualSquared);
            }

            if (breakdown && !cycleConverged) {
                iterationCount = completedIterations;
                status = SparseIterativeStatus.Breakdown;
                return false;
            }
        }

        iterationCount = completedIterations;
        status = currentResidualNorm <= threshold
            ? SparseIterativeStatus.Succeeded
            : SparseIterativeStatus.MaximumIterationsReached;
        return currentResidualNorm <= threshold;
    }

    private static bool TrySolveArnoldiSystem(
        ReadOnlySpan<double> hessenberg,
        int leadingDimension,
        Span<double> rightHandSide,
        int order) {
        for (var row = order - 1; row >= 0; row--) {
            var value = rightHandSide[row];
            for (var column = row + 1; column < order; column++) {
                value -= hessenberg[(column * leadingDimension) + row] *
                    rightHandSide[column];
            }

            var diagonal = hessenberg[(row * leadingDimension) + row];
            if (diagonal == 0) {
                return false;
            }

            rightHandSide[row] = value / diagonal;
            if (!double.IsFinite(rightHandSide[row])) {
                return false;
            }
        }

        return true;
    }

    private static double EuclideanNorm(ReadOnlySpan<double> values, int count) {
        var norm = 0.0;
        for (var index = 0; index < count; index++) {
            var value = values[index];
            if (!double.IsFinite(value)) {
                return double.NaN;
            }

            norm = ElementaryFunctions.Hypot(norm, value);
        }

        return norm;
    }

    private static double Dot(
        ReadOnlySpan<double> first,
        ReadOnlySpan<double> second,
        int count) {
        var result = 0.0;
        for (var index = 0; index < count; index++) {
            result += first[index] * second[index];
        }

        return result;
    }

    private static void AddScaledInPlace(
        Span<double> destination,
        ReadOnlySpan<double> source,
        double scale,
        int count) {
        for (var index = 0; index < count; index++) {
            destination[index] += scale * source[index];
        }
    }
}
