// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Eigenvalues and optional right eigenvectors of a general real square
/// matrix in row-major storage.
/// </summary>
/// <remarks>
/// The matrix is reduced to upper Hessenberg form with orthogonal similarity
/// transformations and then reduced with a complex-shifted QR iteration.
/// Eigenvalues are returned in the order produced by the deflation process;
/// because the complex numbers have no canonical total order, callers should
/// not depend on that order.
///
/// Eigenvectors are normalized to unit Euclidean length and are determined by
/// a singular-value null-space calculation. Repeated or defective eigenvalues
/// can yield an incomplete or numerically sensitive eigenbasis; use a
/// structure-aware decomposition when that distinction matters. Hessenberg
/// reduction and shifted QR follow Golub and Van Loan, <i>Matrix
/// Computations</i>, 4th ed. (2013), ISBN 978-1-4214-0794-4.
/// </remarks>
public sealed class GeneralEigenDecomposition {
    private const int MaximumIterationsPerOrder = 200;
    private readonly double[] _source;
    private readonly Complex[] _eigenvalues;

    public GeneralEigenDecomposition(ReadOnlySpan<double> source, int order) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order);
        var elementCount = checked(order * order);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source does not contain enough matrix values.",
                nameof(source));
        }

        _source = new double[elementCount];
        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(source[index])) {
                throw new ArgumentException(
                    "The source matrix must contain only finite values.",
                    nameof(source));
            }

            _source[index] = source[index];
        }

        Order = order;
        _eigenvalues = ComputeEigenvalues(_source, order);
    }

    public int Order { get; }

    /// <summary>
    /// Gets the eigenvalues in the order produced by QR deflation.
    /// </summary>
    public ReadOnlySpan<Complex> Eigenvalues => _eigenvalues;

    /// <summary>
    /// Computes normalized right eigenvectors in a row-major order-by-order
    /// matrix whose columns correspond to <see cref="Eigenvalues"/>.
    /// </summary>
    public Complex[] ComputeEigenvectors() {
        var elementCount = checked(Order * Order);
        var result = new Complex[elementCount];
        if (Order == 1) {
            result[0] = Complex.One;
            return result;
        }

        var shifted = new Complex[elementCount];
        for (var component = 0; component < Order; component++) {
            var eigenvalue = _eigenvalues[component];
            for (var row = 0; row < Order; row++) {
                var rowOffset = row * Order;
                for (var column = 0; column < Order; column++) {
                    shifted[rowOffset + column] = row == column
                        ? new Complex(
                            _source[rowOffset + column] - eigenvalue.Real,
                            -eigenvalue.Imaginary)
                        : new Complex(_source[rowOffset + column], 0);
                }
            }

            var svd = new ComplexSvdDecomposition(shifted, Order, Order);
            var vectors = svd.GetV();
            var smallestComponent = Order - 1;
            var maximumMagnitude = 0.0;
            for (var row = 0; row < Order; row++) {
                maximumMagnitude = Math.Max(
                    maximumMagnitude,
                    vectors[(row * Order) + smallestComponent].Magnitude);
            }

            if (maximumMagnitude == 0 || !double.IsFinite(maximumMagnitude)) {
                throw new ArithmeticException(
                    "An eigenvector null space could not be determined.");
            }

            var inverseMaximum = 1 / maximumMagnitude;
            for (var row = 0; row < Order; row++) {
                result[(row * Order) + component] =
                    vectors[(row * Order) + smallestComponent] * inverseMaximum;
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the residual norm <c>||A v - lambda v||</c> for each
    /// eigenpair. The returned residual uses the same column ordering as
    /// <see cref="ComputeEigenvectors"/>.
    /// </summary>
    public double[] ComputeResidualNorms(ReadOnlySpan<Complex> eigenvectors) {
        var elementCount = checked(Order * Order);
        if (eigenvectors.Length < elementCount) {
            throw new ArgumentException(
                "The eigenvector matrix does not contain enough values.",
                nameof(eigenvectors));
        }

        var residuals = new double[Order];
        for (var component = 0; component < Order; component++) {
            var sum = 0.0;
            for (var row = 0; row < Order; row++) {
                var value = Complex.Zero;
                for (var column = 0; column < Order; column++) {
                    value += new Complex(
                        _source[(row * Order) + column],
                        0) * eigenvectors[(column * Order) + component];
                }

                value -= _eigenvalues[component] *
                    eigenvectors[(row * Order) + component];
                sum += value.MagnitudeSquared;
            }

            residuals[component] = Math.Sqrt(sum);
        }

        return residuals;
    }

    private static Complex[] ComputeEigenvalues(
        ReadOnlySpan<double> source,
        int order) {
        var realHessenberg = source.ToArray();
        ReduceToHessenberg(realHessenberg, order);
        var matrix = new Complex[realHessenberg.Length];
        for (var index = 0; index < realHessenberg.Length; index++) {
            matrix[index] = new Complex(realHessenberg[index], 0);
        }

        var active = order - 1;
        var iterations = 0;
        var maximumIterations = MaximumIterationsPerOrder * order;
        while (active > 0) {
            var blockStart = active;
            while (blockStart > 0 &&
                !IsNegligibleSubdiagonal(matrix, order, blockStart)) {
                blockStart--;
            }

            if (blockStart == active) {
                matrix[(active * order) + active - 1] = Complex.Zero;
                active--;
                continue;
            }

            if (blockStart == active - 1) {
                DeflatePair(matrix, order, active - 1, active);
                active -= 2;
                continue;
            }

            iterations++;
            if (iterations > maximumIterations) {
                throw new ArithmeticException(
                    "The general eigenvalue QR iteration did not converge.");
            }

            var shift = SelectShift(matrix, order, active);
            for (var index = blockStart; index <= active; index++) {
                matrix[(index * order) + index] -= shift;
            }

            for (var column = blockStart; column < active; column++) {
                ApplyGivens(matrix, order, column, blockStart, active);
            }

            for (var index = blockStart; index <= active; index++) {
                matrix[(index * order) + index] += shift;
            }
        }

        var eigenvalues = new Complex[order];
        for (var index = 0; index < order; index++) {
            eigenvalues[index] = matrix[(index * order) + index];
        }

        return eigenvalues;
    }

    private static void ReduceToHessenberg(Span<double> matrix, int order) {
        var vector = new double[order];
        for (var column = 0; column < order - 2; column++) {
            var vectorLength = order - column - 1;
            var scale = 0.0;
            for (var row = column + 1; row < order; row++) {
                scale += Math.Abs(matrix[(row * order) + column]);
            }

            if (scale == 0) {
                continue;
            }

            var sumSquares = 0.0;
            for (var row = column + 1; row < order; row++) {
                var value = matrix[(row * order) + column];
                sumSquares += value * value;
            }

            var norm = Math.Sqrt(sumSquares);
            var pivot = matrix[((column + 1) * order) + column];
            var reflectorPivot = pivot + Math.CopySign(norm, pivot);
            vector[0] = reflectorPivot;
            var normalizationSquared = reflectorPivot * reflectorPivot;
            for (var index = 1; index < vectorLength; index++) {
                var value = matrix[((column + 1 + index) * order) + column];
                vector[index] = value;
                normalizationSquared += value * value;
            }

            if (normalizationSquared == 0 ||
                !double.IsFinite(normalizationSquared)) {
                throw new ArithmeticException(
                    "The Hessenberg reduction encountered an invalid reflector.");
            }

            var inverseNormalization = 1 / Math.Sqrt(normalizationSquared);
            for (var index = 0; index < vectorLength; index++) {
                vector[index] *= inverseNormalization;
            }

            for (var targetColumn = column; targetColumn < order; targetColumn++) {
                var dot = 0.0;
                for (var index = 0; index < vectorLength; index++) {
                    dot += vector[index] *
                        matrix[((column + 1 + index) * order) + targetColumn];
                }

                var adjustment = 2 * dot;
                for (var index = 0; index < vectorLength; index++) {
                    matrix[((column + 1 + index) * order) + targetColumn] -=
                        adjustment * vector[index];
                }
            }

            for (var row = 0; row < order; row++) {
                var dot = 0.0;
                for (var index = 0; index < vectorLength; index++) {
                    dot += matrix[(row * order) + column + 1 + index] *
                        vector[index];
                }

                var adjustment = 2 * dot;
                for (var index = 0; index < vectorLength; index++) {
                    matrix[(row * order) + column + 1 + index] -=
                        adjustment * vector[index];
                }
            }

            matrix[((column + 1) * order) + column] =
                pivot < 0 ? norm : -norm;
            for (var row = column + 2; row < order; row++) {
                matrix[(row * order) + column] = 0;
            }
        }
    }

    private static bool IsNegligibleSubdiagonal(
        ReadOnlySpan<Complex> matrix,
        int order,
        int row) {
        var subdiagonal = matrix[(row * order) + row - 1].Magnitude;
        if (subdiagonal == 0) {
            return true;
        }

        var diagonalScale =
            matrix[((row - 1) * order) + row - 1].Magnitude +
            matrix[(row * order) + row].Magnitude;
        return subdiagonal <=
            16 * ScalarMath.MachineEpsilon * diagonalScale;
    }

    private static Complex SelectShift(
        ReadOnlySpan<Complex> matrix,
        int order,
        int active) {
        var first = matrix[((active - 1) * order) + active - 1];
        var second = matrix[(active * order) + active];
        var offDiagonalProduct = matrix[((active - 1) * order) + active] *
            matrix[(active * order) + active - 1];
        var discriminant = ((first + second) * (first + second)) -
            (4 * ((first * second) - offDiagonalProduct));
        var root = ComplexMath.Sqrt(discriminant);
        var firstRoot = 0.5 * (first + second + root);
        var secondRoot = 0.5 * (first + second - root);
        return (firstRoot - second).Magnitude <=
            (secondRoot - second).Magnitude ? firstRoot : secondRoot;
    }

    private static void DeflatePair(
        Span<Complex> matrix,
        int order,
        int first,
        int second) {
        var firstDiagonal = matrix[(first * order) + first];
        var secondDiagonal = matrix[(second * order) + second];
        var offDiagonalProduct = matrix[(first * order) + second] *
            matrix[(second * order) + first];
        var trace = firstDiagonal + secondDiagonal;
        var determinant = (firstDiagonal * secondDiagonal) - offDiagonalProduct;
        var discriminant = (trace * trace) - (4 * determinant);
        var root = ComplexMath.Sqrt(discriminant);
        matrix[(first * order) + first] = 0.5 * (trace + root);
        matrix[(second * order) + second] = 0.5 * (trace - root);
        matrix[(second * order) + first] = Complex.Zero;
    }

    private static void ApplyGivens(
        Span<Complex> matrix,
        int order,
        int pivot,
        int blockStart,
        int active) {
        var first = matrix[(pivot * order) + pivot];
        var second = matrix[((pivot + 1) * order) + pivot];
        if (second == Complex.Zero) {
            return;
        }

        if (first == Complex.Zero) {
            for (var column = 0; column < order; column++) {
                var upper = matrix[(pivot * order) + column];
                var lower = matrix[((pivot + 1) * order) + column];
                var phase = lower / lower.Magnitude;
                matrix[(pivot * order) + column] = phase.Conjugate() * lower;
                matrix[((pivot + 1) * order) + column] = -phase * upper;
            }

            for (var row = blockStart; row <= active; row++) {
                var upper = matrix[(row * order) + pivot];
                var lower = matrix[(row * order) + pivot + 1];
                var phase = second / second.Magnitude;
                matrix[(row * order) + pivot] = phase * upper;
                matrix[(row * order) + pivot + 1] = lower;
            }

            return;
        }

        var radius = FloatingPointMath.Hypot(first.Magnitude, second.Magnitude);
        var cosine = first.Magnitude / radius;
        var sine = (first / first.Magnitude) * second.Conjugate() / radius;
        var conjugateSine = sine.Conjugate();
        for (var column = 0; column < order; column++) {
            var upper = matrix[(pivot * order) + column];
            var lower = matrix[((pivot + 1) * order) + column];
            matrix[(pivot * order) + column] =
                (cosine * upper) + (sine * lower);
            matrix[((pivot + 1) * order) + column] =
                (-conjugateSine * upper) + (cosine * lower);
        }

        for (var row = 0; row < order; row++) {
            var upper = matrix[(row * order) + pivot];
            var lower = matrix[(row * order) + pivot + 1];
            matrix[(row * order) + pivot] =
                (cosine * upper) + (conjugateSine * lower);
            matrix[(row * order) + pivot + 1] =
                (-sine * upper) + (cosine * lower);
        }
    }
}
