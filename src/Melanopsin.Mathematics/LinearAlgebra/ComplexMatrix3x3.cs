// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A three-by-three matrix over <see cref="Complex"/> values using row-major
/// logical storage and column-vector multiplication.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct ComplexMatrix3x3 :
    IAdditionOperators<
        ComplexMatrix3x3,
        ComplexMatrix3x3,
        ComplexMatrix3x3>,
    ISubtractionOperators<
        ComplexMatrix3x3,
        ComplexMatrix3x3,
        ComplexMatrix3x3>,
    IUnaryNegationOperators<ComplexMatrix3x3, ComplexMatrix3x3>,
    IMultiplyOperators<
        ComplexMatrix3x3,
        ComplexMatrix3x3,
        ComplexMatrix3x3>,
    IMultiplyOperators<
        ComplexMatrix3x3,
        ComplexVector3,
        ComplexVector3>,
    IMultiplyOperators<
        ComplexMatrix3x3,
        Complex,
        ComplexMatrix3x3>,
    IDivisionOperators<ComplexMatrix3x3, Complex, ComplexMatrix3x3>,
    IAdditiveIdentity<ComplexMatrix3x3, ComplexMatrix3x3>,
    IMultiplicativeIdentity<ComplexMatrix3x3, ComplexMatrix3x3>,
    IEqualityOperators<ComplexMatrix3x3, ComplexMatrix3x3, bool> {
    public ComplexMatrix3x3(
        Complex m11,
        Complex m12,
        Complex m13,
        Complex m21,
        Complex m22,
        Complex m23,
        Complex m31,
        Complex m32,
        Complex m33) {
        M11 = m11;
        M12 = m12;
        M13 = m13;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        M31 = m31;
        M32 = m32;
        M33 = m33;
    }

    public static ComplexMatrix3x3 Zero => default;

    public static ComplexMatrix3x3 Identity => new(
        Complex.One,
        Complex.Zero,
        Complex.Zero,
        Complex.Zero,
        Complex.One,
        Complex.Zero,
        Complex.Zero,
        Complex.Zero,
        Complex.One);

    public static ComplexMatrix3x3 AdditiveIdentity => Zero;

    public static ComplexMatrix3x3 MultiplicativeIdentity => Identity;

    public Complex M11 { get; }

    public Complex M12 { get; }

    public Complex M13 { get; }

    public Complex M21 { get; }

    public Complex M22 { get; }

    public Complex M23 { get; }

    public Complex M31 { get; }

    public Complex M32 { get; }

    public Complex M33 { get; }

    public ComplexVector3 Row1 => new(M11, M12, M13);

    public ComplexVector3 Row2 => new(M21, M22, M23);

    public ComplexVector3 Row3 => new(M31, M32, M33);

    public ComplexVector3 Column1 => new(M11, M21, M31);

    public ComplexVector3 Column2 => new(M12, M22, M32);

    public ComplexVector3 Column3 => new(M13, M23, M33);

    public Complex Determinant =>
        (M11 * ((M22 * M33) - (M23 * M32))) -
        (M12 * ((M21 * M33) - (M23 * M31))) +
        (M13 * ((M21 * M32) - (M22 * M31)));

    public Complex Trace => M11 + M22 + M33;

    public double FrobeniusNorm => EuclideanNorm();

    public bool IsFinite =>
        ComplexMath.IsFinite(M11) &&
        ComplexMath.IsFinite(M12) &&
        ComplexMath.IsFinite(M13) &&
        ComplexMath.IsFinite(M21) &&
        ComplexMath.IsFinite(M22) &&
        ComplexMath.IsFinite(M23) &&
        ComplexMath.IsFinite(M31) &&
        ComplexMath.IsFinite(M32) &&
        ComplexMath.IsFinite(M33);

    public static ComplexMatrix3x3 FromStorage(
        ReadOnlySpan<Complex> source,
        MatrixStorageOrder storageOrder) {
        if (source.Length < 9) {
            throw new ArgumentException(
                "A 3x3 matrix requires nine values.",
                nameof(source));
        }

        return storageOrder switch {
            MatrixStorageOrder.RowMajor => new ComplexMatrix3x3(
                source[0], source[1], source[2],
                source[3], source[4], source[5],
                source[6], source[7], source[8]),
            MatrixStorageOrder.ColumnMajor => new ComplexMatrix3x3(
                source[0], source[3], source[6],
                source[1], source[4], source[7],
                source[2], source[5], source[8]),
            _ => throw new ArgumentOutOfRangeException(nameof(storageOrder)),
        };
    }

    public ComplexVector3 Transform(ComplexVector3 value) {
        return new ComplexVector3(
            (M11 * value.X) + (M12 * value.Y) + (M13 * value.Z),
            (M21 * value.X) + (M22 * value.Y) + (M23 * value.Z),
            (M31 * value.X) + (M32 * value.Y) + (M33 * value.Z));
    }

    public void CopyTo(
        Span<Complex> destination,
        MatrixStorageOrder storageOrder) {
        if (destination.Length < 9) {
            throw new ArgumentException(
                "A 3x3 matrix requires nine destination values.",
                nameof(destination));
        }

        switch (storageOrder) {
            case MatrixStorageOrder.RowMajor:
                destination[0] = M11;
                destination[1] = M12;
                destination[2] = M13;
                destination[3] = M21;
                destination[4] = M22;
                destination[5] = M23;
                destination[6] = M31;
                destination[7] = M32;
                destination[8] = M33;
                break;
            case MatrixStorageOrder.ColumnMajor:
                destination[0] = M11;
                destination[1] = M21;
                destination[2] = M31;
                destination[3] = M12;
                destination[4] = M22;
                destination[5] = M32;
                destination[6] = M13;
                destination[7] = M23;
                destination[8] = M33;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(storageOrder));
        }
    }

    public ComplexMatrix3x3 Transpose() {
        return new ComplexMatrix3x3(
            M11, M21, M31,
            M12, M22, M32,
            M13, M23, M33);
    }

    public ComplexMatrix3x3 Conjugate() {
        return new ComplexMatrix3x3(
            M11.Conjugate(),
            M12.Conjugate(),
            M13.Conjugate(),
            M21.Conjugate(),
            M22.Conjugate(),
            M23.Conjugate(),
            M31.Conjugate(),
            M32.Conjugate(),
            M33.Conjugate());
    }

    public ComplexMatrix3x3 ConjugateTranspose() {
        return new ComplexMatrix3x3(
            M11.Conjugate(),
            M21.Conjugate(),
            M31.Conjugate(),
            M12.Conjugate(),
            M22.Conjugate(),
            M32.Conjugate(),
            M13.Conjugate(),
            M23.Conjugate(),
            M33.Conjugate());
    }

    /// <summary>
    /// Determines whether the matrix is Hermitian within combined absolute and
    /// relative tolerances.
    /// </summary>
    public bool IsHermitian(
        double tolerance = ScalarMath.DefaultTolerance) {
        return ComplexMath.IsNearlyZero(M11.Imaginary, tolerance) &&
            ComplexMath.IsNearlyZero(M22.Imaginary, tolerance) &&
            ComplexMath.IsNearlyZero(M33.Imaginary, tolerance) &&
            ComplexMath.IsNearlyEqual(M12, M21.Conjugate(), tolerance, tolerance) &&
            ComplexMath.IsNearlyEqual(M13, M31.Conjugate(), tolerance, tolerance) &&
            ComplexMath.IsNearlyEqual(M23, M32.Conjugate(), tolerance, tolerance);
    }

    public ComplexMatrix3x3 Invert() {
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
    public bool TryInvert(out ComplexMatrix3x3 inverse) {
        return TryInvert(out inverse, out _);
    }

    /// <summary>
    /// Attempts to invert the matrix and reports whether the input, determinant,
    /// or output was non-finite or the determinant was exactly zero.
    /// </summary>
    public bool TryInvert(
        out ComplexMatrix3x3 inverse,
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

        var c11 = (M22 * M33) - (M23 * M32);
        var c12 = (M13 * M32) - (M12 * M33);
        var c13 = (M12 * M23) - (M13 * M22);
        var c21 = (M23 * M31) - (M21 * M33);
        var c22 = (M11 * M33) - (M13 * M31);
        var c23 = (M13 * M21) - (M11 * M23);
        var c31 = (M21 * M32) - (M22 * M31);
        var c32 = (M12 * M31) - (M11 * M32);
        var c33 = (M11 * M22) - (M12 * M21);
        var inverseDeterminant = Complex.One / determinant;
        var i11 = c11 * inverseDeterminant;
        var i12 = c12 * inverseDeterminant;
        var i13 = c13 * inverseDeterminant;
        var i21 = c21 * inverseDeterminant;
        var i22 = c22 * inverseDeterminant;
        var i23 = c23 * inverseDeterminant;
        var i31 = c31 * inverseDeterminant;
        var i32 = c32 * inverseDeterminant;
        var i33 = c33 * inverseDeterminant;
        if (!ComplexMath.IsFinite(i11) ||
            !ComplexMath.IsFinite(i12) ||
            !ComplexMath.IsFinite(i13) ||
            !ComplexMath.IsFinite(i21) ||
            !ComplexMath.IsFinite(i22) ||
            !ComplexMath.IsFinite(i23) ||
            !ComplexMath.IsFinite(i31) ||
            !ComplexMath.IsFinite(i32) ||
            !ComplexMath.IsFinite(i33)) {
            report = new ComplexMatrixOperationReport(
                ComplexMatrixOperation.Inversion,
                ComplexMatrixOperationStatus.NonFiniteOutput);
            return false;
        }

        inverse = new ComplexMatrix3x3(
            i11, i12, i13,
            i21, i22, i23,
            i31, i32, i33);
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

    public static ComplexMatrix3x3 Add(
        ComplexMatrix3x3 left,
        ComplexMatrix3x3 right) {
        return new ComplexMatrix3x3(
            left.M11 + right.M11,
            left.M12 + right.M12,
            left.M13 + right.M13,
            left.M21 + right.M21,
            left.M22 + right.M22,
            left.M23 + right.M23,
            left.M31 + right.M31,
            left.M32 + right.M32,
            left.M33 + right.M33);
    }

    public static ComplexMatrix3x3 Subtract(
        ComplexMatrix3x3 left,
        ComplexMatrix3x3 right) {
        return new ComplexMatrix3x3(
            left.M11 - right.M11,
            left.M12 - right.M12,
            left.M13 - right.M13,
            left.M21 - right.M21,
            left.M22 - right.M22,
            left.M23 - right.M23,
            left.M31 - right.M31,
            left.M32 - right.M32,
            left.M33 - right.M33);
    }

    public static ComplexMatrix3x3 Negate(ComplexMatrix3x3 value) {
        return new ComplexMatrix3x3(
            -value.M11,
            -value.M12,
            -value.M13,
            -value.M21,
            -value.M22,
            -value.M23,
            -value.M31,
            -value.M32,
            -value.M33);
    }

    public static ComplexMatrix3x3 Multiply(
        ComplexMatrix3x3 left,
        ComplexMatrix3x3 right) {
        return new ComplexMatrix3x3(
            (left.M11 * right.M11) +
                (left.M12 * right.M21) +
                (left.M13 * right.M31),
            (left.M11 * right.M12) +
                (left.M12 * right.M22) +
                (left.M13 * right.M32),
            (left.M11 * right.M13) +
                (left.M12 * right.M23) +
                (left.M13 * right.M33),
            (left.M21 * right.M11) +
                (left.M22 * right.M21) +
                (left.M23 * right.M31),
            (left.M21 * right.M12) +
                (left.M22 * right.M22) +
                (left.M23 * right.M32),
            (left.M21 * right.M13) +
                (left.M22 * right.M23) +
                (left.M23 * right.M33),
            (left.M31 * right.M11) +
                (left.M32 * right.M21) +
                (left.M33 * right.M31),
            (left.M31 * right.M12) +
                (left.M32 * right.M22) +
                (left.M33 * right.M32),
            (left.M31 * right.M13) +
                (left.M32 * right.M23) +
                (left.M33 * right.M33));
    }

    public static ComplexVector3 Multiply(
        ComplexMatrix3x3 matrix,
        ComplexVector3 value) {
        return matrix.Transform(value);
    }

    public static ComplexMatrix3x3 Multiply(
        ComplexMatrix3x3 matrix,
        Complex scale) {
        return new ComplexMatrix3x3(
            matrix.M11 * scale,
            matrix.M12 * scale,
            matrix.M13 * scale,
            matrix.M21 * scale,
            matrix.M22 * scale,
            matrix.M23 * scale,
            matrix.M31 * scale,
            matrix.M32 * scale,
            matrix.M33 * scale);
    }

    public static ComplexMatrix3x3 Multiply(
        Complex scale,
        ComplexMatrix3x3 matrix) {
        return Multiply(matrix, scale);
    }

    public static ComplexMatrix3x3 Divide(
        ComplexMatrix3x3 matrix,
        Complex divisor) {
        return new ComplexMatrix3x3(
            matrix.M11 / divisor,
            matrix.M12 / divisor,
            matrix.M13 / divisor,
            matrix.M21 / divisor,
            matrix.M22 / divisor,
            matrix.M23 / divisor,
            matrix.M31 / divisor,
            matrix.M32 / divisor,
            matrix.M33 / divisor);
    }

    public static ComplexMatrix3x3 operator +(
        ComplexMatrix3x3 left,
        ComplexMatrix3x3 right) {
        return Add(left, right);
    }

    public static ComplexMatrix3x3 operator -(
        ComplexMatrix3x3 left,
        ComplexMatrix3x3 right) {
        return Subtract(left, right);
    }

    public static ComplexMatrix3x3 operator -(ComplexMatrix3x3 value) {
        return Negate(value);
    }

    public static ComplexMatrix3x3 operator *(
        ComplexMatrix3x3 left,
        ComplexMatrix3x3 right) {
        return Multiply(left, right);
    }

    public static ComplexVector3 operator *(
        ComplexMatrix3x3 matrix,
        ComplexVector3 value) {
        return Multiply(matrix, value);
    }

    public static ComplexMatrix3x3 operator *(
        ComplexMatrix3x3 matrix,
        Complex scale) {
        return Multiply(matrix, scale);
    }

    public static ComplexMatrix3x3 operator *(
        Complex scale,
        ComplexMatrix3x3 matrix) {
        return Multiply(scale, matrix);
    }

    public static ComplexMatrix3x3 operator /(
        ComplexMatrix3x3 matrix,
        Complex divisor) {
        return Divide(matrix, divisor);
    }

    private double EuclideanNorm() {
        var magnitude11 = M11.Magnitude;
        var magnitude12 = M12.Magnitude;
        var magnitude13 = M13.Magnitude;
        var magnitude21 = M21.Magnitude;
        var magnitude22 = M22.Magnitude;
        var magnitude23 = M23.Magnitude;
        var magnitude31 = M31.Magnitude;
        var magnitude32 = M32.Magnitude;
        var magnitude33 = M33.Magnitude;
        var scale = Math.Max(
            Math.Max(
                Math.Max(magnitude11, magnitude12),
                Math.Max(magnitude13, magnitude21)),
            Math.Max(
                Math.Max(magnitude22, magnitude23),
                Math.Max(magnitude31, Math.Max(magnitude32, magnitude33))));
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
        var n13 = magnitude13 / scale;
        var n21 = magnitude21 / scale;
        var n22 = magnitude22 / scale;
        var n23 = magnitude23 / scale;
        var n31 = magnitude31 / scale;
        var n32 = magnitude32 / scale;
        var n33 = magnitude33 / scale;
        return scale * Math.Sqrt(
            (n11 * n11) +
            (n12 * n12) +
            (n13 * n13) +
            (n21 * n21) +
            (n22 * n22) +
            (n23 * n23) +
            (n31 * n31) +
            (n32 * n32) +
            (n33 * n33));
    }
}
