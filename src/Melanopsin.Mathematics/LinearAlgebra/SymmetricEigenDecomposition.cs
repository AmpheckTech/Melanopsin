// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// An eigenvalue decomposition of a real symmetric row-major matrix.
/// </summary>
/// <remarks>
/// Eigenvalues are sorted in ascending order. The corresponding orthonormal
/// eigenvectors are columns of the stored matrix, so
/// <c>A = V diag(lambda) V^T</c>. Symmetry is checked exactly by default. A
/// caller may supply a non-negative relative symmetry tolerance; the check
/// then uses the largest absolute source entry as its scale. The cyclic Jacobi
/// rotations follow Jacobi (1846),
/// <see href="https://doi.org/10.1515/crll.1846.30.51">doi:10.1515/crll.1846.30.51</see>;
/// ordering and convergence follow Golub and Van Loan, <i>Matrix Computations</i>,
/// 4th ed. (2013), ISBN 978-1-4214-0794-4.
/// </remarks>
public sealed class SymmetricEigenDecomposition {
    private const int MaximumSweeps = 100;
    private readonly double[] _eigenvalues;
    private readonly double[] _eigenvectors;

    public SymmetricEigenDecomposition(
        ReadOnlySpan<double> source,
        int order,
        double relativeSymmetryTolerance = 0) {
        ValidateRelativeTolerance(relativeSymmetryTolerance);
        var elementCount = ValidateSource(source, order);
        var maximumMagnitude = 0.0;
        for (var index = 0; index < elementCount; index++) {
            maximumMagnitude = Math.Max(maximumMagnitude, Math.Abs(source[index]));
        }

        var symmetryScale = maximumMagnitude;
        for (var row = 0; row < order; row++) {
            for (var column = row + 1; column < order; column++) {
                var asymmetry = Math.Abs(
                    source[(row * order) + column] -
                    source[(column * order) + row]);
                if (asymmetry > relativeSymmetryTolerance * symmetryScale) {
                    throw new ArgumentException(
                        "The source matrix must be symmetric within the requested tolerance.",
                        nameof(source));
                }
            }
        }

        Order = order;
        _eigenvalues = new double[order];
        _eigenvectors = new double[elementCount];
        for (var index = 0; index < order; index++) {
            _eigenvectors[(index * order) + index] = 1;
        }

        JacobiRotate(source[..elementCount], order);
        SortEigenpairs(order);
    }

    public int Order { get; }

    public ReadOnlySpan<double> Eigenvalues => _eigenvalues;

    public double SmallestEigenvalue => _eigenvalues[0];

    public double LargestEigenvalue => _eigenvalues[^1];

    public bool IsPositiveDefinite => _eigenvalues[0] > 0;

    public double ConditionNumber {
        get {
            var smallest = Math.Abs(_eigenvalues[0]);
            return smallest == 0
                ? double.PositiveInfinity
                : Math.Abs(_eigenvalues[^1]) / smallest;
        }
    }

    public void CopyEigenvaluesTo(Span<double> destination) {
        CopyBuffer(_eigenvalues, destination, nameof(destination));
    }

    /// <summary>
    /// Gets the eigenvectors as an order-by-order row-major matrix whose
    /// columns correspond to the sorted eigenvalues.
    /// </summary>
    public double[] GetEigenvectors() {
        return _eigenvectors.AsSpan().ToArray();
    }

    public void CopyEigenvectorsTo(Span<double> destination) {
        CopyBuffer(_eigenvectors, destination, nameof(destination));
    }

    /// <summary>
    /// Reconstructs <c>V diag(lambda) V^T</c>.
    /// </summary>
    public void Reconstruct(Span<double> destination) {
        var elementCount = checked(Order * Order);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        for (var row = 0; row < Order; row++) {
            for (var column = 0; column < Order; column++) {
                var value = 0.0;
                for (var component = 0; component < Order; component++) {
                    value += _eigenvectors[(row * Order) + component] *
                        _eigenvalues[component] *
                        _eigenvectors[(column * Order) + component];
                }

                destination[(row * Order) + column] = value;
            }
        }
    }

    /// <summary>
    /// Inverts the source matrix through its spectral decomposition if every
    /// eigenvalue is exactly nonzero.
    /// </summary>
    public bool TryInvert(Span<double> destination) {
        return TryInvert(destination, out _);
    }

    /// <summary>
    /// Inverts the source matrix through its spectral decomposition and
    /// reports the numerical termination status.
    /// </summary>
    /// <param name="destination">Receives the row-major inverse.</param>
    /// <param name="report">Receives the numerical termination status.</param>
    /// <returns><see langword="true"/> exactly when <c>report.Succeeded</c> is true.</returns>
    public bool TryInvert(
        Span<double> destination,
        out LinearAlgebraOperationReport report) {
        var elementCount = checked(Order * Order);
        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                nameof(destination));
        }

        destination[..elementCount].Clear();
        for (var component = 0; component < Order; component++) {
            if (_eigenvalues[component] == 0) {
                report = new LinearAlgebraOperationReport(
                    LinearAlgebraOperation.SymmetricEigenInvert,
                    LinearAlgebraStatus.Singular);
                return false;
            }
        }

        for (var row = 0; row < Order; row++) {
            for (var column = 0; column < Order; column++) {
                var value = 0.0;
                for (var component = 0; component < Order; component++) {
                    value += _eigenvectors[(row * Order) + component] *
                        _eigenvectors[(column * Order) + component] /
                        _eigenvalues[component];
                }

                if (!double.IsFinite(value)) {
                    destination[..elementCount].Clear();
                    report = new LinearAlgebraOperationReport(
                        LinearAlgebraOperation.SymmetricEigenInvert,
                        LinearAlgebraStatus.NonFiniteOutput);
                    return false;
                }

                destination[(row * Order) + column] = value;
            }
        }

        report = new LinearAlgebraOperationReport(
            LinearAlgebraOperation.SymmetricEigenInvert,
            LinearAlgebraStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Inverts the source matrix through its spectral decomposition.
    /// </summary>
    public double[] Invert() {
        var inverse = new double[checked(Order * Order)];
        if (!TryInvert(inverse)) {
            throw new ArithmeticException(
                "The matrix is singular or its inverse is non-finite.");
        }

        return inverse;
    }

    private static int ValidateSource(ReadOnlySpan<double> source, int order) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order);
        var elementCount = checked(order * order);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough matrix values.",
                nameof(source));
        }

        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(source[index])) {
                throw new ArgumentException(
                    "The source matrix must contain only finite values.",
                    nameof(source));
            }
        }

        return elementCount;
    }

    private static void ValidateRelativeTolerance(double tolerance) {
        if (!double.IsFinite(tolerance) || tolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
    }

    private void JacobiRotate(ReadOnlySpan<double> source, int order) {
        var matrix = source.ToArray();
        var converged = false;
        for (var sweep = 0; sweep < MaximumSweeps; sweep++) {
            var maximumOffDiagonal = 0.0;
            for (var first = 0; first < order - 1; first++) {
                for (var second = first + 1; second < order; second++) {
                    var offDiagonal = matrix[(first * order) + second];
                    maximumOffDiagonal = Math.Max(
                        maximumOffDiagonal,
                        Math.Abs(offDiagonal));
                    if (offDiagonal == 0) {
                        continue;
                    }

                    Rotate(matrix, order, first, second, offDiagonal);
                }
            }

            var norm = MatrixNorms.FrobeniusNorm(matrix, order, order);
            if (maximumOffDiagonal <=
                16 * ScalarMath.MachineEpsilon * norm) {
                converged = true;
                break;
            }
        }

        if (!converged) {
            throw new ArithmeticException(
                "The symmetric Jacobi eigenvalue iteration did not converge.");
        }

        for (var index = 0; index < order; index++) {
            _eigenvalues[index] = matrix[(index * order) + index];
        }
    }

    private void Rotate(
        Span<double> matrix,
        int order,
        int first,
        int second,
        double offDiagonal) {
        var firstDiagonal = matrix[(first * order) + first];
        var secondDiagonal = matrix[(second * order) + second];
        var angle = (secondDiagonal - firstDiagonal) / (2 * offDiagonal);
        var tangent = Math.CopySign(
            1 / (Math.Abs(angle) + FloatingPointMath.Hypot(1, angle)),
            angle);
        var cosine = 1 / Math.Sqrt(1 + (tangent * tangent));
        var sine = cosine * tangent;
        var cosineSquared = cosine * cosine;
        var sineSquared = sine * sine;
        var twoSineCosine = 2 * sine * cosine;
        for (var index = 0; index < order; index++) {
            if (index == first || index == second) {
                continue;
            }

            var firstValue = matrix[(index * order) + first];
            var secondValue = matrix[(index * order) + second];
            var rotatedFirst = (cosine * firstValue) - (sine * secondValue);
            var rotatedSecond = (sine * firstValue) + (cosine * secondValue);
            matrix[(index * order) + first] = rotatedFirst;
            matrix[(first * order) + index] = rotatedFirst;
            matrix[(index * order) + second] = rotatedSecond;
            matrix[(second * order) + index] = rotatedSecond;
        }

        matrix[(first * order) + first] =
            (cosineSquared * firstDiagonal) -
            (twoSineCosine * offDiagonal) +
            (sineSquared * secondDiagonal);
        matrix[(second * order) + second] =
            (sineSquared * firstDiagonal) +
            (twoSineCosine * offDiagonal) +
            (cosineSquared * secondDiagonal);
        matrix[(first * order) + second] = 0;
        matrix[(second * order) + first] = 0;
        for (var row = 0; row < order; row++) {
            var firstValue = _eigenvectors[(row * order) + first];
            var secondValue = _eigenvectors[(row * order) + second];
            _eigenvectors[(row * order) + first] =
                (cosine * firstValue) - (sine * secondValue);
            _eigenvectors[(row * order) + second] =
                (sine * firstValue) + (cosine * secondValue);
        }
    }

    private void SortEigenpairs(int order) {
        // Selection sort keeps the eigenvalue/vector pairing in place. The
        // decomposition is already O(order^3), so the O(order^2) ordering pass
        // is dominated by the Jacobi rotations and avoids temporary arrays.
        for (var destination = 0; destination < order - 1; destination++) {
            var source = destination;
            for (var candidate = destination + 1; candidate < order; candidate++) {
                if (_eigenvalues[candidate] < _eigenvalues[source]) {
                    source = candidate;
                }
            }

            if (source == destination) {
                continue;
            }

            (_eigenvalues[destination], _eigenvalues[source]) =
                (_eigenvalues[source], _eigenvalues[destination]);
            for (var row = 0; row < order; row++) {
                var destinationIndex = (row * order) + destination;
                var sourceIndex = (row * order) + source;
                (_eigenvectors[destinationIndex], _eigenvectors[sourceIndex]) =
                    (_eigenvectors[sourceIndex], _eigenvectors[destinationIndex]);
            }
        }
    }

    private static void CopyBuffer(
        ReadOnlySpan<double> source,
        Span<double> destination,
        string parameterName) {
        if (destination.Length < source.Length) {
            throw new ArgumentException(
                "The destination does not contain enough values.",
                parameterName);
        }

        source.CopyTo(destination);
    }
}
