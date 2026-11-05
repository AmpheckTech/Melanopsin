// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Pure matrix functions for small and medium dense real matrices.
/// </summary>
/// <remarks>
/// Inputs and outputs are row-major square matrices using column-vector
/// semantics. Algorithms use explicitly allocated work buffers so callers
/// never need to reason about hidden mutation. Singular intermediate factors
/// yield <see langword="false"/> through the Try variants. The matrix
/// exponential uses scaling and squaring with the degree-13 Padé threshold and
/// coefficients from Higham (2005),
/// <see href="https://doi.org/10.1137/04061101X">doi:10.1137/04061101X</see>.
/// General accuracy considerations follow Higham, <i>Accuracy and Stability of
/// Numerical Algorithms</i>, 2nd ed. (2002),
/// <see href="https://doi.org/10.1137/1.9780898718027">doi:10.1137/1.9780898718027</see>.
/// </remarks>
public static partial class MatrixFunctions {
    private const double PadéTheta = 5.371920351148152;
    private const int SquareRootMaximumIterations = 100;
    private const int LogarithmMaximumIterations = 100;
    private const int LogarithmSeriesMaximumTerms = 128;
    private static readonly double[] PadéCoefficients = [
        64764752532480000,
        32382376266240000,
        7771770303897600,
        1187353796428800,
        129060195264000,
        10559470521600,
        670442572800,
        33522128640,
        1323241920,
        40840800,
        960960,
        16380,
        182,
        1,
    ];

    /// <summary>
    /// Computes the matrix exponential with scaling and squaring and a
    /// [13/13] Padé approximant.
    /// </summary>
    public static double[] Exp(ReadOnlySpan<double> source, int order) {
        var destination = new double[checked(order * order)];
        if (!TryExp(source, order, destination, out var report)) {
            ThrowForFailure(report, "The matrix exponential could not be computed.");
        }

        return destination;
    }

    /// <summary>
    /// Attempts the matrix exponential without exposing numerical failure as an
    /// exception.
    /// </summary>
    public static bool TryExp(
        ReadOnlySpan<double> source,
        int order,
        Span<double> destination) {
        return TryExp(source, order, destination, out _);
    }

    /// <summary>
    /// Attempts the matrix exponential and reports the numerical failure reason.
    /// </summary>
    public static bool TryExp(
        ReadOnlySpan<double> source,
        int order,
        Span<double> destination,
        out MatrixFunctionReport report) {
        Validate(source, order, destination);
        var elementCount = checked(order * order);
        destination[..elementCount].Clear();
        if (!IsFiniteMatrix(source, elementCount)) {
            report = Failure(
                MatrixFunctionKind.Exponential,
                MatrixFunctionStatus.NonFiniteInput);
            return false;
        }

        var matrix = source[..elementCount].ToArray();
        var norm = OneNorm(matrix, order);
        if (!double.IsFinite(norm)) {
            report = Failure(
                MatrixFunctionKind.Exponential,
                MatrixFunctionStatus.NormOverflow);
            return false;
        }

        var squaringEstimate = norm <= PadéTheta
            ? 0
            : Math.Ceiling(Math.Log2(norm / PadéTheta));
        if (squaringEstimate > 1023) {
            report = Failure(
                MatrixFunctionKind.Exponential,
                MatrixFunctionStatus.ScalingLimitExceeded);
            return false;
        }

        var squarings = (int)squaringEstimate;
        if (squarings > 0) {
            Scale(matrix, Math.Pow(2, -squarings));
        }

        var identity = new double[elementCount];
        SetIdentity(identity, order);
        var squared = new double[elementCount];
        var fourth = new double[elementCount];
        var sixth = new double[elementCount];
        DenseMatrix.Multiply(matrix, order, order, matrix, order, order, squared);
        DenseMatrix.Multiply(squared, order, order, squared, order, order, fourth);
        DenseMatrix.Multiply(fourth, order, order, squared, order, order, sixth);

        var polynomialFirst = CombinePowers(
            squared,
            fourth,
            sixth,
            PadéCoefficients[9],
            PadéCoefficients[11],
            PadéCoefficients[13]);
        var polynomialFirstProduct = new double[elementCount];
        DenseMatrix.Multiply(
            sixth,
            order,
            order,
            polynomialFirst,
            order,
            order,
            polynomialFirstProduct);
        polynomialFirstProduct.AsSpan().CopyTo(polynomialFirst);
        AddScaled(polynomialFirst, sixth, PadéCoefficients[7]);
        AddScaled(polynomialFirst, fourth, PadéCoefficients[5]);
        AddScaled(polynomialFirst, squared, PadéCoefficients[3]);
        AddScaled(polynomialFirst, identity, PadéCoefficients[1]);
        var numerator = new double[elementCount];
        DenseMatrix.Multiply(
            matrix,
            order,
            order,
            polynomialFirst,
            order,
            order,
            numerator);

        var polynomialSecond = CombinePowers(
            squared,
            fourth,
            sixth,
            PadéCoefficients[8],
            PadéCoefficients[10],
            PadéCoefficients[12]);
        var polynomialSecondProduct = new double[elementCount];
        DenseMatrix.Multiply(
            sixth,
            order,
            order,
            polynomialSecond,
            order,
            order,
            polynomialSecondProduct);
        polynomialSecondProduct.AsSpan().CopyTo(polynomialSecond);
        AddScaled(polynomialSecond, sixth, PadéCoefficients[6]);
        AddScaled(polynomialSecond, fourth, PadéCoefficients[4]);
        AddScaled(polynomialSecond, squared, PadéCoefficients[2]);
        AddScaled(polynomialSecond, identity, PadéCoefficients[0]);

        var sum = new double[elementCount];
        var difference = new double[elementCount];
        for (var index = 0; index < elementCount; index++) {
            sum[index] = polynomialSecond[index] + numerator[index];
            difference[index] = polynomialSecond[index] - numerator[index];
        }

        var result = new double[elementCount];
        var factorization = new LuDecomposition(difference, order);
        var column = new double[order];
        for (var columnIndex = 0; columnIndex < order; columnIndex++) {
            for (var row = 0; row < order; row++) {
                column[row] = sum[(row * order) + columnIndex];
            }

            if (!factorization.TrySolve(
                column,
                result.AsSpan(columnIndex * order, order))) {
                destination[..elementCount].Clear();
                report = Failure(
                    MatrixFunctionKind.Exponential,
                    MatrixFunctionStatus.SingularIntermediateFactor);
                return false;
            }
        }

        for (var iteration = 0; iteration < squarings; iteration++) {
            var next = new double[elementCount];
            DenseMatrix.Multiply(
                result,
                order,
                order,
                result,
                order,
                order,
                next);
            result = next;
        }

        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(result[index])) {
                destination[..elementCount].Clear();
                report = Failure(
                    MatrixFunctionKind.Exponential,
                    MatrixFunctionStatus.NonFiniteOutput);
                return false;
            }

            destination[index] = result[index];
        }

        report = Success(MatrixFunctionKind.Exponential);
        return true;
    }

    /// <summary>
    /// Computes a principal matrix square root with the Denman-Beavers
    /// iteration.
    /// </summary>
    public static double[] Sqrt(ReadOnlySpan<double> source, int order) {
        var destination = new double[checked(order * order)];
        if (!TrySqrt(source, order, destination, out var report)) {
            ThrowForFailure(
                report,
                "The principal matrix square root could not be computed.");
        }

        return destination;
    }

    /// <summary>
    /// Attempts the principal matrix square root without exposing numerical
    /// failure as an exception.
    /// </summary>
    public static bool TrySqrt(
        ReadOnlySpan<double> source,
        int order,
        Span<double> destination) {
        return TrySqrt(source, order, destination, out _);
    }

    /// <summary>
    /// Attempts the principal matrix square root and reports the numerical
    /// failure reason.
    /// </summary>
    public static bool TrySqrt(
        ReadOnlySpan<double> source,
        int order,
        Span<double> destination,
        out MatrixFunctionReport report) {
        Validate(source, order, destination);
        var elementCount = checked(order * order);
        destination[..elementCount].Clear();
        if (!IsFiniteMatrix(source, elementCount)) {
            report = Failure(
                MatrixFunctionKind.PrincipalSquareRoot,
                MatrixFunctionStatus.NonFiniteInput);
            return false;
        }

        var current = source[..elementCount].ToArray();
        var inverse = new double[elementCount];
        SetIdentity(inverse, order);
        var previousChange = double.PositiveInfinity;
        for (var iteration = 0; iteration < SquareRootMaximumIterations; iteration++) {
            var currentInverse = new double[elementCount];
            var inverseInverse = new double[elementCount];
            if (!TryInvertLu(
                    current,
                    order,
                    currentInverse,
                    out var currentInverseStatus)) {
                report = Failure(
                    MatrixFunctionKind.PrincipalSquareRoot,
                    currentInverseStatus == LinearAlgebraStatus.Singular
                        ? MatrixFunctionStatus.SingularIntermediateFactor
                        : MatrixFunctionStatus.NonFiniteIntermediate);
                return false;
            }

            if (!TryInvertLu(
                    inverse,
                    order,
                    inverseInverse,
                    out var inverseInverseStatus)) {
                report = Failure(
                    MatrixFunctionKind.PrincipalSquareRoot,
                    inverseInverseStatus == LinearAlgebraStatus.Singular
                        ? MatrixFunctionStatus.SingularIntermediateFactor
                        : MatrixFunctionStatus.NonFiniteIntermediate);
                return false;
            }

            var nextCurrent = new double[elementCount];
            var nextInverse = new double[elementCount];
            for (var index = 0; index < elementCount; index++) {
                nextCurrent[index] = 0.5 * (current[index] + inverseInverse[index]);
                nextInverse[index] = 0.5 * (inverse[index] + currentInverse[index]);
                if (!double.IsFinite(nextCurrent[index]) ||
                    !double.IsFinite(nextInverse[index])) {
                    report = Failure(
                        MatrixFunctionKind.PrincipalSquareRoot,
                        MatrixFunctionStatus.NonFiniteIntermediate);
                    return false;
                }
            }

            var change = FrobeniusDifference(nextCurrent, current);
            current = nextCurrent;
            inverse = nextInverse;
            if (change <=
                32 * ScalarMath.MachineEpsilon * FrobeniusNorm(current)) {
                current.AsSpan().CopyTo(destination);
                report = Success(MatrixFunctionKind.PrincipalSquareRoot);
                return true;
            }

            if (change >= previousChange) {
                report = Failure(
                    MatrixFunctionKind.PrincipalSquareRoot,
                    MatrixFunctionStatus.IterationDiverged);
                return false;
            }

            previousChange = change;
        }

        report = Failure(
            MatrixFunctionKind.PrincipalSquareRoot,
            MatrixFunctionStatus.IterationNotConverged);
        return false;
    }

    /// <summary>
    /// Computes a real matrix logarithm with inverse scaling and squaring.
    /// </summary>
    /// <remarks>
    /// The series is defined for matrices whose spectrum avoids the closed
    /// negative real axis. For a negative real eigenvalue the result is not a
    /// real matrix and this method returns <see langword="false"/>.
    /// </remarks>
    public static double[] Log(ReadOnlySpan<double> source, int order) {
        var destination = new double[checked(order * order)];
        if (!TryLog(source, order, destination, out var report)) {
            ThrowForFailure(
                report,
                "A real matrix logarithm could not be computed.");
        }

        return destination;
    }

    /// <summary>
    /// Attempts the real matrix logarithm without exposing numerical failure as
    /// an exception.
    /// </summary>
    public static bool TryLog(
        ReadOnlySpan<double> source,
        int order,
        Span<double> destination) {
        return TryLog(source, order, destination, out _);
    }

    /// <summary>
    /// Attempts the real matrix logarithm and reports the numerical failure
    /// reason.
    /// </summary>
    public static bool TryLog(
        ReadOnlySpan<double> source,
        int order,
        Span<double> destination,
        out MatrixFunctionReport report) {
        Validate(source, order, destination);
        var elementCount = checked(order * order);
        destination[..elementCount].Clear();
        if (!IsFiniteMatrix(source, elementCount)) {
            report = Failure(
                MatrixFunctionKind.RealLogarithm,
                MatrixFunctionStatus.NonFiniteInput);
            return false;
        }

        var current = source[..elementCount].ToArray();
        var squarings = 0;
        var identity = new double[elementCount];
        SetIdentity(identity, order);
        while (FrobeniusDifference(current, identity) > 0.5) {
            var next = new double[elementCount];
            if (!TrySqrt(current, order, next)) {
                report = Failure(
                    MatrixFunctionKind.RealLogarithm,
                    MatrixFunctionStatus.SquareRootUnavailable);
                return false;
            }

            current = next;
            squarings++;
            if (squarings > LogarithmMaximumIterations) {
                report = Failure(
                    MatrixFunctionKind.RealLogarithm,
                    MatrixFunctionStatus.IterationNotConverged);
                return false;
            }
        }

        var difference = new double[elementCount];
        for (var index = 0; index < elementCount; index++) {
            difference[index] = current[index] - identity[index];
        }

        var logarithm = new double[elementCount];
        var power = difference.ToArray();
        var converged = false;
        for (var degree = 1; degree <= LogarithmSeriesMaximumTerms; degree++) {
            var scale = (degree % 2 == 0 ? -1.0 : 1.0) / degree;
            AddScaled(logarithm, power, scale);
            var logarithmNorm = FrobeniusNorm(logarithm);
            var termNorm = FrobeniusNorm(power) / degree;
            if (!double.IsFinite(logarithmNorm) || !double.IsFinite(termNorm)) {
                report = Failure(
                    MatrixFunctionKind.RealLogarithm,
                    MatrixFunctionStatus.NonFiniteIntermediate);
                return false;
            }

            if (termNorm <=
                32 * ScalarMath.MachineEpsilon * logarithmNorm) {
                converged = true;
                break;
            }

            if (degree == LogarithmSeriesMaximumTerms) {
                break;
            }

            var nextPower = new double[elementCount];
            DenseMatrix.Multiply(
                power,
                order,
                order,
                difference,
                order,
                order,
                nextPower);
            if (!IsFiniteMatrix(nextPower, elementCount)) {
                report = Failure(
                    MatrixFunctionKind.RealLogarithm,
                    MatrixFunctionStatus.NonFiniteIntermediate);
                return false;
            }

            power = nextPower;
        }

        if (!converged) {
            report = Failure(
                MatrixFunctionKind.RealLogarithm,
                MatrixFunctionStatus.IterationNotConverged);
            return false;
        }

        var factor = Math.Pow(2, squarings);
        for (var index = 0; index < elementCount; index++) {
            var value = logarithm[index] * factor;
            if (!double.IsFinite(value)) {
                destination[..elementCount].Clear();
                report = Failure(
                    MatrixFunctionKind.RealLogarithm,
                    MatrixFunctionStatus.NonFiniteOutput);
                return false;
            }

            destination[index] = value;
        }

        report = Success(MatrixFunctionKind.RealLogarithm);
        return true;
    }

    /// <summary>
    /// Raises a square real matrix to an integer power.
    /// </summary>
    public static double[] Pow(
        ReadOnlySpan<double> source,
        int order,
        int exponent) {
        var destination = new double[checked(order * order)];
        if (!TryPow(source, order, exponent, destination, out var report)) {
            ThrowForFailure(report, "The matrix power could not be computed.");
        }

        return destination;
    }

    /// <summary>
    /// Attempts an integer matrix power without exposing numerical failure as
    /// an exception.
    /// </summary>
    public static bool TryPow(
        ReadOnlySpan<double> source,
        int order,
        int exponent,
        Span<double> destination) {
        return TryPow(source, order, exponent, destination, out _);
    }

    /// <summary>
    /// Attempts an integer matrix power and reports the numerical failure
    /// reason.
    /// </summary>
    public static bool TryPow(
        ReadOnlySpan<double> source,
        int order,
        int exponent,
        Span<double> destination,
        out MatrixFunctionReport report) {
        Validate(source, order, destination);
        var elementCount = checked(order * order);
        destination[..elementCount].Clear();
        if (!IsFiniteMatrix(source, elementCount)) {
            report = Failure(
                MatrixFunctionKind.IntegerPower,
                MatrixFunctionStatus.NonFiniteInput);
            return false;
        }

        if (exponent < 0) {
            var inverse = new double[elementCount];
            if (!TryInvertLu(
                    source[..elementCount],
                    order,
                    inverse,
                    out var inverseStatus)) {
                report = Failure(
                    MatrixFunctionKind.IntegerPower,
                    inverseStatus == LinearAlgebraStatus.Singular
                        ? MatrixFunctionStatus.SingularInput
                        : MatrixFunctionStatus.NonFiniteOutput);
                return false;
            }

            if (!TryPowPositive(
                inverse,
                order,
                -(long)exponent,
                destination)) {
                report = Failure(
                    MatrixFunctionKind.IntegerPower,
                    MatrixFunctionStatus.NonFiniteOutput);
                return false;
            }

            report = Success(MatrixFunctionKind.IntegerPower);
            return true;
        }

        if (!TryPowPositive(source[..elementCount], order, exponent, destination)) {
            report = Failure(
                MatrixFunctionKind.IntegerPower,
                MatrixFunctionStatus.NonFiniteOutput);
            return false;
        }

        report = Success(MatrixFunctionKind.IntegerPower);
        return true;
    }

    private static bool TryPowPositive(
        ReadOnlySpan<double> source,
        int order,
        long exponent,
        Span<double> destination) {
        var elementCount = checked(order * order);
        var result = new double[elementCount];
        SetIdentity(result, order);
        var factor = source.ToArray();
        while (exponent > 0) {
            if ((exponent & 1) != 0) {
                var product = new double[elementCount];
                DenseMatrix.Multiply(
                    result,
                    order,
                    order,
                    factor,
                    order,
                    order,
                    product);
                result = product;
            }

            exponent >>= 1;
            if (exponent == 0) {
                break;
            }

            var square = new double[elementCount];
            DenseMatrix.Multiply(
                factor,
                order,
                order,
                factor,
                order,
                order,
                square);
            factor = square;
        }

        for (var index = 0; index < elementCount; index++) {
            if (!double.IsFinite(result[index])) {
                destination[..elementCount].Clear();
                return false;
            }
        }

        result.AsSpan().CopyTo(destination);
        return true;
    }

    private static double[] CombinePowers(
        ReadOnlySpan<double> squared,
        ReadOnlySpan<double> fourth,
        ReadOnlySpan<double> sixth,
        double coefficientSquared,
        double coefficientFourth,
        double coefficientSixth) {
        var result = new double[squared.Length];
        AddScaled(result, squared, coefficientSquared);
        AddScaled(result, fourth, coefficientFourth);
        AddScaled(result, sixth, coefficientSixth);
        return result;
    }

    private static void AddScaled(
        Span<double> destination,
        ReadOnlySpan<double> source,
        double scale) {
        for (var index = 0; index < destination.Length; index++) {
            destination[index] += source[index] * scale;
        }
    }

    private static double OneNorm(ReadOnlySpan<double> matrix, int order) {
        var maximum = 0.0;
        for (var column = 0; column < order; column++) {
            var sum = 0.0;
            for (var row = 0; row < order; row++) {
                sum += Math.Abs(matrix[(row * order) + column]);
            }

            maximum = Math.Max(maximum, sum);
        }

        return maximum;
    }

    private static double FrobeniusNorm(ReadOnlySpan<double> matrix) {
        var sum = 0.0;
        for (var index = 0; index < matrix.Length; index++) {
            sum += matrix[index] * matrix[index];
        }

        return Math.Sqrt(sum);
    }

    private static double FrobeniusDifference(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right) {
        var sum = 0.0;
        for (var index = 0; index < left.Length; index++) {
            var difference = left[index] - right[index];
            sum += difference * difference;
        }

        return Math.Sqrt(sum);
    }

    private static void Scale(Span<double> matrix, double scale) {
        for (var index = 0; index < matrix.Length; index++) {
            matrix[index] *= scale;
        }
    }

    private static void SetIdentity(Span<double> matrix, int order) {
        matrix.Clear();
        for (var index = 0; index < order; index++) {
            matrix[(index * order) + index] = 1;
        }
    }

    private static bool IsFiniteMatrix(ReadOnlySpan<double> values, int count) {
        for (var index = 0; index < count; index++) {
            if (!double.IsFinite(values[index])) {
                return false;
            }
        }

        return true;
    }

    private static MatrixFunctionReport Success(MatrixFunctionKind function) {
        return new MatrixFunctionReport(
            function,
            MatrixFunctionStatus.Succeeded);
    }

    private static bool TryInvertLu(
        ReadOnlySpan<double> source,
        int order,
        Span<double> inverse,
        out LinearAlgebraStatus status) {
        var workspace = new double[order];
        try {
            var decomposition = new LuDecomposition(source, order);
            if (decomposition.TryInvert(
                    inverse,
                    workspace,
                    out var report)) {
                status = LinearAlgebraStatus.Succeeded;
                return true;
            }

            status = report.Status;
            return false;
        } catch (ArithmeticException) {
            inverse.Clear();
            status = LinearAlgebraStatus.NonFiniteOutput;
            return false;
        }
    }

    private static MatrixFunctionReport Failure(
        MatrixFunctionKind function,
        MatrixFunctionStatus status) {
        return new MatrixFunctionReport(function, status);
    }

    private static void ThrowForFailure(
        MatrixFunctionReport report,
        string message) {
        if (report.Status == MatrixFunctionStatus.NonFiniteInput) {
            throw new ArgumentException(
                "The source matrix must contain only finite values.");
        }

        throw new ArithmeticException(message);
    }

    private static void Validate(
        ReadOnlySpan<double> source,
        int order,
        Span<double> destination) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order);
        var elementCount = checked(order * order);
        if (source.Length < elementCount) {
            throw new ArgumentException(
                "The source matrix does not contain enough values.",
                nameof(source));
        }

        if (destination.Length < elementCount) {
            throw new ArgumentException(
                "The destination matrix does not contain enough values.",
                nameof(destination));
        }

        if (source.Overlaps(destination)) {
            throw new ArgumentException(
                "The destination must not overlap the source.",
                nameof(destination));
        }

    }
}
