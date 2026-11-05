// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A two-by-two matrix over <see cref="Complex"/> values using row-major
/// logical storage and column-vector multiplication.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct ComplexMatrix2x2 :
    IAdditionOperators<
        ComplexMatrix2x2,
        ComplexMatrix2x2,
        ComplexMatrix2x2>,
    ISubtractionOperators<
        ComplexMatrix2x2,
        ComplexMatrix2x2,
        ComplexMatrix2x2>,
    IUnaryNegationOperators<ComplexMatrix2x2, ComplexMatrix2x2>,
    IMultiplyOperators<
        ComplexMatrix2x2,
        ComplexMatrix2x2,
        ComplexMatrix2x2>,
    IMultiplyOperators<
        ComplexMatrix2x2,
        ComplexVector2,
        ComplexVector2>,
    IMultiplyOperators<
        ComplexMatrix2x2,
        Complex,
        ComplexMatrix2x2>,
    IDivisionOperators<ComplexMatrix2x2, Complex, ComplexMatrix2x2>,
    IAdditiveIdentity<ComplexMatrix2x2, ComplexMatrix2x2>,
    IMultiplicativeIdentity<ComplexMatrix2x2, ComplexMatrix2x2>,
    IEqualityOperators<ComplexMatrix2x2, ComplexMatrix2x2, bool> {
    public ComplexMatrix2x2(
        Complex m11,
        Complex m12,
        Complex m21,
        Complex m22) {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
    }

    public static ComplexMatrix2x2 Zero => default;

    public static ComplexMatrix2x2 Identity => new(
        Complex.One,
        Complex.Zero,
        Complex.Zero,
        Complex.One);

    public static ComplexMatrix2x2 AdditiveIdentity => Zero;

    public static ComplexMatrix2x2 MultiplicativeIdentity => Identity;

    public Complex M11 { get; }

    public Complex M12 { get; }

    public Complex M21 { get; }

    public Complex M22 { get; }

    public ComplexVector2 Row1 => new(M11, M12);

    public ComplexVector2 Row2 => new(M21, M22);

    public ComplexVector2 Column1 => new(M11, M21);

    public ComplexVector2 Column2 => new(M12, M22);

    public Complex Determinant => (M11 * M22) - (M12 * M21);

    public Complex Trace => M11 + M22;

    public double FrobeniusNorm => EuclideanNorm(M11, M12, M21, M22);

    public bool IsFinite =>
        ComplexMath.IsFinite(M11) &&
        ComplexMath.IsFinite(M12) &&
        ComplexMath.IsFinite(M21) &&
        ComplexMath.IsFinite(M22);

    public static ComplexMatrix2x2 FromStorage(
        ReadOnlySpan<Complex> source,
        MatrixStorageOrder storageOrder) {
        if (source.Length < 4) {
            throw new ArgumentException(
                "A 2x2 matrix requires four values.",
                nameof(source));
        }

        return storageOrder switch {
            MatrixStorageOrder.RowMajor => new ComplexMatrix2x2(
                source[0], source[1],
                source[2], source[3]),
            MatrixStorageOrder.ColumnMajor => new ComplexMatrix2x2(
                source[0], source[2],
                source[1], source[3]),
            _ => throw new ArgumentOutOfRangeException(nameof(storageOrder)),
        };
    }

    public ComplexVector2 Transform(ComplexVector2 value) {
        return new ComplexVector2(
            (M11 * value.X) + (M12 * value.Y),
            (M21 * value.X) + (M22 * value.Y));
    }

    public void CopyTo(
        Span<Complex> destination,
        MatrixStorageOrder storageOrder) {
        if (destination.Length < 4) {
            throw new ArgumentException(
                "A 2x2 matrix requires four destination values.",
                nameof(destination));
        }

        switch (storageOrder) {
            case MatrixStorageOrder.RowMajor:
                destination[0] = M11;
                destination[1] = M12;
                destination[2] = M21;
                destination[3] = M22;
                break;
            case MatrixStorageOrder.ColumnMajor:
                destination[0] = M11;
                destination[1] = M21;
                destination[2] = M12;
                destination[3] = M22;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(storageOrder));
        }
    }

    public ComplexMatrix2x2 Transpose() {
        return new ComplexMatrix2x2(M11, M21, M12, M22);
    }

    public ComplexMatrix2x2 Conjugate() {
        return new ComplexMatrix2x2(
            M11.Conjugate(),
            M12.Conjugate(),
            M21.Conjugate(),
            M22.Conjugate());
    }

    public ComplexMatrix2x2 ConjugateTranspose() {
        return new ComplexMatrix2x2(
            M11.Conjugate(),
            M21.Conjugate(),
            M12.Conjugate(),
            M22.Conjugate());
    }

    /// <summary>
    /// Determines whether the matrix is Hermitian within combined absolute and
    /// relative tolerances.
    /// </summary>
    public bool IsHermitian(
        double tolerance = ScalarMath.DefaultTolerance) {
        return ComplexMath.IsNearlyZero(M11.Imaginary, tolerance) &&
            ComplexMath.IsNearlyZero(M22.Imaginary, tolerance) &&
            ComplexMath.IsNearlyEqual(
                M12,
                M21.Conjugate(),
                tolerance,
                tolerance);
    }

    public ComplexMatrix2x2 Invert() {
        if (!TryInvert(out var inverse)) {
            throw new ArithmeticException(
                "The matrix is singular or its inverse is non-finite.");
        }

        return inverse;
    }

    /// <summary>
    /// Attempts to invert the matrix without applying an absolute
    /// near-singularity threshold.
    /// </summary>
    /// <remarks>
    /// Determinant magnitude is scale-dependent. Inversion fails only for an
    /// exact zero determinant in double precision, a non-finite determinant,
    /// or non-finite inverse elements. Use
    /// <see cref="TryEstimateConditionNumber(out double)"/> when numerical accuracy must be
    /// assessed by the caller.
    /// </remarks>
    public bool TryInvert(out ComplexMatrix2x2 inverse) {
        return TryInvert(out inverse, out _);
    }

    /// <summary>
    /// Attempts to invert the matrix and reports whether the input, determinant,
    /// or output was non-finite or the determinant was exactly zero.
    /// </summary>
    public bool TryInvert(
        out ComplexMatrix2x2 inverse,
        out ComplexMatrixOperationReport report) {
        inverse = default;
        if (!IsFinite) {
            report = new ComplexMatrixOperationReport(
                ComplexMatrixOperation.Inversion,
                ComplexMatrixOperationStatus.NonFiniteInput);
            return false;
        }

        var determinant = Determinant;
        if (!ComplexMath.IsFinite(determinant)) {
            report = new ComplexMatrixOperationReport(
                ComplexMatrixOperation.Inversion,
                ComplexMatrixOperationStatus.NonFiniteDeterminant);
            return false;
        }

        if (determinant == Complex.Zero) {
            report = new ComplexMatrixOperationReport(
                ComplexMatrixOperation.Inversion,
                ComplexMatrixOperationStatus.Singular);
            return false;
        }

        var inverseDeterminant = Complex.One / determinant;
        var i11 = M22 * inverseDeterminant;
        var i12 = -M12 * inverseDeterminant;
        var i21 = -M21 * inverseDeterminant;
        var i22 = M11 * inverseDeterminant;
        if (!ComplexMath.IsFinite(i11) ||
            !ComplexMath.IsFinite(i12) ||
            !ComplexMath.IsFinite(i21) ||
            !ComplexMath.IsFinite(i22)) {
            report = new ComplexMatrixOperationReport(
                ComplexMatrixOperation.Inversion,
                ComplexMatrixOperationStatus.NonFiniteOutput);
            return false;
        }

        inverse = new ComplexMatrix2x2(i11, i12, i21, i22);
        report = new ComplexMatrixOperationReport(
            ComplexMatrixOperation.Inversion,
            ComplexMatrixOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Estimates the condition number as the product of the matrix and inverse
    /// Frobenius norms and reports why the estimate could not be produced.
    /// </summary>
    public bool TryEstimateConditionNumber(out double conditionNumber) {
        return TryEstimateConditionNumber(out conditionNumber, out _);
    }

    /// <summary>
    /// Estimates the condition number and reports why the estimate could not be
    /// produced.
    /// </summary>
    public bool TryEstimateConditionNumber(
        out double conditionNumber,
        out ComplexMatrixOperationReport report) {
        if (!TryInvert(out var inverse, out var inverseReport)) {
            conditionNumber = double.PositiveInfinity;
            report = new ComplexMatrixOperationReport(
                ComplexMatrixOperation.ConditionEstimate,
                inverseReport.Status);
            return false;
        }

        conditionNumber = FrobeniusNorm * inverse.FrobeniusNorm;
        if (!double.IsFinite(conditionNumber)) {
            conditionNumber = double.PositiveInfinity;
            report = new ComplexMatrixOperationReport(
                ComplexMatrixOperation.ConditionEstimate,
                ComplexMatrixOperationStatus.NonFiniteOutput);
            return false;
        }

        report = new ComplexMatrixOperationReport(
            ComplexMatrixOperation.ConditionEstimate,
            ComplexMatrixOperationStatus.Succeeded);
        return true;
    }

    public static ComplexMatrix2x2 Add(
        ComplexMatrix2x2 left,
        ComplexMatrix2x2 right) {
        return new ComplexMatrix2x2(
            left.M11 + right.M11,
            left.M12 + right.M12,
            left.M21 + right.M21,
            left.M22 + right.M22);
    }

    public static ComplexMatrix2x2 Subtract(
        ComplexMatrix2x2 left,
        ComplexMatrix2x2 right) {
        return new ComplexMatrix2x2(
            left.M11 - right.M11,
            left.M12 - right.M12,
            left.M21 - right.M21,
            left.M22 - right.M22);
    }

    public static ComplexMatrix2x2 Negate(ComplexMatrix2x2 value) {
        return new ComplexMatrix2x2(
            -value.M11,
            -value.M12,
            -value.M21,
            -value.M22);
    }

    public static ComplexMatrix2x2 Multiply(
        ComplexMatrix2x2 left,
        ComplexMatrix2x2 right) {
        return new ComplexMatrix2x2(
            (left.M11 * right.M11) + (left.M12 * right.M21),
            (left.M11 * right.M12) + (left.M12 * right.M22),
            (left.M21 * right.M11) + (left.M22 * right.M21),
            (left.M21 * right.M12) + (left.M22 * right.M22));
    }

    public static ComplexVector2 Multiply(
        ComplexMatrix2x2 matrix,
        ComplexVector2 value) {
        return matrix.Transform(value);
    }

    public static ComplexMatrix2x2 Multiply(
        ComplexMatrix2x2 matrix,
        Complex scale) {
        return new ComplexMatrix2x2(
            matrix.M11 * scale,
            matrix.M12 * scale,
            matrix.M21 * scale,
            matrix.M22 * scale);
    }

    public static ComplexMatrix2x2 Multiply(
        Complex scale,
        ComplexMatrix2x2 matrix) {
        return Multiply(matrix, scale);
    }

    public static ComplexMatrix2x2 Divide(
        ComplexMatrix2x2 matrix,
        Complex divisor) {
        return new ComplexMatrix2x2(
            matrix.M11 / divisor,
            matrix.M12 / divisor,
            matrix.M21 / divisor,
            matrix.M22 / divisor);
    }

    public static ComplexMatrix2x2 operator +(
        ComplexMatrix2x2 left,
        ComplexMatrix2x2 right) {
        return Add(left, right);
    }

    public static ComplexMatrix2x2 operator -(
        ComplexMatrix2x2 left,
        ComplexMatrix2x2 right) {
        return Subtract(left, right);
    }

    public static ComplexMatrix2x2 operator -(ComplexMatrix2x2 value) {
        return Negate(value);
    }

    public static ComplexMatrix2x2 operator *(
        ComplexMatrix2x2 left,
        ComplexMatrix2x2 right) {
        return Multiply(left, right);
    }

    public static ComplexVector2 operator *(
        ComplexMatrix2x2 matrix,
        ComplexVector2 value) {
        return Multiply(matrix, value);
    }

    public static ComplexMatrix2x2 operator *(
        ComplexMatrix2x2 matrix,
        Complex scale) {
        return Multiply(matrix, scale);
    }

    public static ComplexMatrix2x2 operator *(
        Complex scale,
        ComplexMatrix2x2 matrix) {
        return Multiply(scale, matrix);
    }

    public static ComplexMatrix2x2 operator /(
        ComplexMatrix2x2 matrix,
        Complex divisor) {
        return Divide(matrix, divisor);
    }

    private static double EuclideanNorm(
        Complex m11,
        Complex m12,
        Complex m21,
        Complex m22) {
        var magnitude11 = m11.Magnitude;
        var magnitude12 = m12.Magnitude;
        var magnitude21 = m21.Magnitude;
        var magnitude22 = m22.Magnitude;
        var scale = Math.Max(
            Math.Max(magnitude11, magnitude12),
            Math.Max(magnitude21, magnitude22));
        if (scale == 0) {
            return 0;
        }

        if (double.IsNaN(scale)) {
            return double.NaN;
        }

        if (!double.IsFinite(scale)) {
            return double.PositiveInfinity;
        }

        var n11 = magnitude11 / scale;
        var n12 = magnitude12 / scale;
        var n21 = magnitude21 / scale;
        var n22 = magnitude22 / scale;
        return scale * Math.Sqrt(
            (n11 * n11) +
            (n12 * n12) +
            (n21 * n21) +
            (n22 * n22));
    }
}
