// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>Identifies a spatial operation that can terminate without a result.</summary>
public enum SpatialOperation {
    /// <summary>Normalization of a two-, three-, or four-component vector.</summary>
    VectorNormalization,

    /// <summary>Inversion of a fixed-size spatial matrix.</summary>
    MatrixInversion,

    /// <summary>Estimation of a fixed-size spatial matrix condition number.</summary>
    MatrixConditionEstimate,

    /// <summary>Normalization of a quaternion.</summary>
    QuaternionNormalization,

    /// <summary>Inversion of a quaternion.</summary>
    QuaternionInversion,

    /// <summary>Construction of a quaternion from an axis and angle.</summary>
    QuaternionAxisAngleConstruction,

    /// <summary>Construction of a quaternion from a rotation matrix.</summary>
    QuaternionRotationMatrixConstruction,

    /// <summary>Construction of a fixed-size matrix from a quaternion.</summary>
    QuaternionMatrixConstruction,

    /// <summary>Rotation of a vector by a quaternion.</summary>
    QuaternionRotation,

    /// <summary>Decomposition of an affine homogeneous matrix.</summary>
    AffineDecomposition,
}

/// <summary>The termination status of a spatial operation.</summary>
public enum SpatialOperationStatus {
    /// <summary>The operation produced a finite, representable result.</summary>
    Succeeded,

    /// <summary>An input contains NaN or infinity where a finite value is required.</summary>
    NonFiniteInput,

    /// <summary>A required vector, axis, quaternion, or scale is exactly zero.</summary>
    DegenerateInput,

    /// <summary>A required linear map has an exactly zero determinant or pivot.</summary>
    Singular,

    /// <summary>A finite input produced a result outside the finite single-precision range.</summary>
    NonFiniteOutput,

    /// <summary>The matrix does not have the affine fourth row required by the operation.</summary>
    NotAffine,

    /// <summary>
    /// The normalized upper-left linear part contains scale or shear that the
    /// requested decomposition cannot represent.
    /// </summary>
    Shear,

    /// <summary>The operation failed numerically for a reason not covered by another status.</summary>
    NumericalFailure,
}

/// <summary>
/// A compact diagnostic report for vector, matrix, quaternion, and affine
/// spatial operations.
/// </summary>
/// <remarks>
/// <para>
/// Matrix singularity means an exactly zero determinant or elimination pivot in
/// the working precision. A finite non-zero determinant is never classified as
/// near-singular by an absolute threshold. Use
/// <see cref="Matrix2x2.TryEstimateConditionNumber(out double, out SpatialOperationReport)"/>,
/// <see cref="Matrix3x3.TryEstimateConditionNumber(out double, out SpatialOperationReport)"/>,
/// or the corresponding four-by-four overload when an application must make an
/// accuracy decision.
/// </para>
/// <para>
/// Affine decomposition uses an explicit relative tolerance for structural
/// classification only. The tolerance does not classify a non-zero determinant,
/// area, or scale as mathematically singular.
/// </para>
/// </remarks>
public readonly record struct SpatialOperationReport {
    internal SpatialOperationReport(
        SpatialOperation operation,
        SpatialOperationStatus status) {
        Operation = operation;
        Status = status;
    }

    /// <summary>Gets the executed operation.</summary>
    public SpatialOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public SpatialOperationStatus Status { get; }

    /// <summary>Gets whether the operation produced a usable result.</summary>
    public bool Succeeded => Status == SpatialOperationStatus.Succeeded;
}

public readonly partial record struct Vector2 {
    /// <summary>Attempts to normalize the vector and reports why normalization failed.</summary>
    public bool TryNormalize(
        out Vector2 result,
        out SpatialOperationReport report) {
        result = default;
        if (!IsFinite) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        double x = X;
        double y = Y;
        var length = Math.Sqrt((x * x) + (y * y));
        if (length == 0) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.DegenerateInput);
            return false;
        }

        var normalizedX = x / length;
        var normalizedY = y / length;
        if (!MatrixNumerics.IsRepresentableAsFloat(normalizedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedY)) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new Vector2((float)normalizedX, (float)normalizedY);
        report = new SpatialOperationReport(
            SpatialOperation.VectorNormalization,
            SpatialOperationStatus.Succeeded);
        return true;
    }
}

public readonly partial record struct Vector3 {
    /// <summary>Attempts to normalize the vector and reports why normalization failed.</summary>
    public bool TryNormalize(
        out Vector3 result,
        out SpatialOperationReport report) {
        result = default;
        if (!IsFinite) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        double x = X;
        double y = Y;
        double z = Z;
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        if (length == 0) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.DegenerateInput);
            return false;
        }

        var normalizedX = x / length;
        var normalizedY = y / length;
        var normalizedZ = z / length;
        if (!MatrixNumerics.IsRepresentableAsFloat(normalizedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedZ)) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new Vector3(
            (float)normalizedX,
            (float)normalizedY,
            (float)normalizedZ);
        report = new SpatialOperationReport(
            SpatialOperation.VectorNormalization,
            SpatialOperationStatus.Succeeded);
        return true;
    }
}

public readonly partial record struct Vector4 {
    /// <summary>Attempts to normalize the vector and reports why normalization failed.</summary>
    public bool TryNormalize(
        out Vector4 result,
        out SpatialOperationReport report) {
        result = default;
        if (!IsFinite) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        double x = X;
        double y = Y;
        double z = Z;
        double w = W;
        var length = Math.Sqrt(
            (x * x) + (y * y) + (z * z) + (w * w));
        if (length == 0) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.DegenerateInput);
            return false;
        }

        var normalizedX = x / length;
        var normalizedY = y / length;
        var normalizedZ = z / length;
        var normalizedW = w / length;
        if (!MatrixNumerics.IsRepresentableAsFloat(normalizedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedZ) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedW)) {
            report = new SpatialOperationReport(
                SpatialOperation.VectorNormalization,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new Vector4(
            (float)normalizedX,
            (float)normalizedY,
            (float)normalizedZ,
            (float)normalizedW);
        report = new SpatialOperationReport(
            SpatialOperation.VectorNormalization,
            SpatialOperationStatus.Succeeded);
        return true;
    }
}

public readonly partial record struct Matrix2x2 {
    /// <summary>
    /// Attempts to invert the matrix and reports exact singularity separately
    /// from non-finite data and unrepresentable output.
    /// </summary>
    public bool TryInvert(
        out Matrix2x2 inverse,
        out SpatialOperationReport report) {
        inverse = default;
        if (!IsFiniteMatrix(this)) {
            report = MatrixReport(
                SpatialOperation.MatrixInversion,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        if (TryInvertCore(out inverse)) {
            report = MatrixReport(
                SpatialOperation.MatrixInversion,
                SpatialOperationStatus.Succeeded);
            return true;
        }

        double m11 = M11;
        double m12 = M12;
        double m21 = M21;
        double m22 = M22;
        var determinant = (m11 * m22) - (m12 * m21);
        report = MatrixReport(
            SpatialOperation.MatrixInversion,
            !double.IsFinite(determinant)
                ? SpatialOperationStatus.NonFiniteOutput
                : determinant == 0
                    ? SpatialOperationStatus.Singular
                    : SpatialOperationStatus.NonFiniteOutput);
        return false;
    }

    /// <summary>
    /// Estimates the Frobenius condition number and reports inversion failures
    /// separately from an unrepresentable estimate.
    /// </summary>
    public bool TryEstimateConditionNumber(
        out double conditionNumber,
        out SpatialOperationReport report) {
        conditionNumber = double.PositiveInfinity;
        if (!TryInvert(out var inverse, out var inverseReport)) {
            report = MatrixReport(
                SpatialOperation.MatrixConditionEstimate,
                inverseReport.Status);
            return false;
        }

        conditionNumber = MatrixNumerics.FrobeniusNorm(this) *
            MatrixNumerics.FrobeniusNorm(inverse);
        if (!double.IsFinite(conditionNumber)) {
            conditionNumber = double.PositiveInfinity;
            report = MatrixReport(
                SpatialOperation.MatrixConditionEstimate,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        report = MatrixReport(
            SpatialOperation.MatrixConditionEstimate,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    private static bool IsFiniteMatrix(Matrix2x2 matrix) {
        return float.IsFinite(matrix.M11) &&
            float.IsFinite(matrix.M12) &&
            float.IsFinite(matrix.M21) &&
            float.IsFinite(matrix.M22);
    }

    private static SpatialOperationReport MatrixReport(
        SpatialOperation operation,
        SpatialOperationStatus status) {
        return new SpatialOperationReport(operation, status);
    }
}

public readonly partial record struct Matrix3x3 {
    /// <summary>
    /// Attempts to invert the matrix and reports exact singularity separately
    /// from non-finite data and unrepresentable output.
    /// </summary>
    public bool TryInvert(
        out Matrix3x3 inverse,
        out SpatialOperationReport report) {
        inverse = default;
        if (!IsFiniteMatrix(this)) {
            report = MatrixReport(
                SpatialOperation.MatrixInversion,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        if (TryInvertCore(out inverse)) {
            report = MatrixReport(
                SpatialOperation.MatrixInversion,
                SpatialOperationStatus.Succeeded);
            return true;
        }

        double m11 = M11;
        double m12 = M12;
        double m13 = M13;
        double m21 = M21;
        double m22 = M22;
        double m23 = M23;
        double m31 = M31;
        double m32 = M32;
        double m33 = M33;
        var determinant =
            (m11 * ((m22 * m33) - (m23 * m32))) -
            (m12 * ((m21 * m33) - (m23 * m31))) +
            (m13 * ((m21 * m32) - (m22 * m31)));
        report = MatrixReport(
            SpatialOperation.MatrixInversion,
            !double.IsFinite(determinant)
                ? SpatialOperationStatus.NonFiniteOutput
                : determinant == 0
                    ? SpatialOperationStatus.Singular
                    : SpatialOperationStatus.NonFiniteOutput);
        return false;
    }

    /// <summary>
    /// Estimates the Frobenius condition number and reports inversion failures
    /// separately from an unrepresentable estimate.
    /// </summary>
    public bool TryEstimateConditionNumber(
        out double conditionNumber,
        out SpatialOperationReport report) {
        conditionNumber = double.PositiveInfinity;
        if (!TryInvert(out var inverse, out var inverseReport)) {
            report = MatrixReport(
                SpatialOperation.MatrixConditionEstimate,
                inverseReport.Status);
            return false;
        }

        conditionNumber = MatrixNumerics.FrobeniusNorm(this) *
            MatrixNumerics.FrobeniusNorm(inverse);
        if (!double.IsFinite(conditionNumber)) {
            conditionNumber = double.PositiveInfinity;
            report = MatrixReport(
                SpatialOperation.MatrixConditionEstimate,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        report = MatrixReport(
            SpatialOperation.MatrixConditionEstimate,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    private static bool IsFiniteMatrix(Matrix3x3 matrix) {
        return float.IsFinite(matrix.M11) &&
            float.IsFinite(matrix.M12) &&
            float.IsFinite(matrix.M13) &&
            float.IsFinite(matrix.M21) &&
            float.IsFinite(matrix.M22) &&
            float.IsFinite(matrix.M23) &&
            float.IsFinite(matrix.M31) &&
            float.IsFinite(matrix.M32) &&
            float.IsFinite(matrix.M33);
    }

    /// <summary>Attempts to construct a rotation matrix from a quaternion.</summary>
    public static bool TryCreateFromQuaternion(
        Quaternion quaternion,
        out Matrix3x3 result) {
        return TryCreateFromQuaternion(quaternion, out result, out _);
    }

    /// <summary>
    /// Attempts to construct a rotation matrix from a quaternion and reports
    /// non-finite or zero quaternion input.
    /// </summary>
    /// <remarks>
    /// The quaternion is normalized internally. A matrix cannot be constructed
    /// from a zero quaternion because it carries no rotation.
    /// </remarks>
    public static bool TryCreateFromQuaternion(
        Quaternion quaternion,
        out Matrix3x3 result,
        out SpatialOperationReport report) {
        result = default;
        if (!quaternion.TryNormalize(out var normalized, out var normalizeReport)) {
            report = MatrixReport(
                SpatialOperation.QuaternionMatrixConstruction,
                normalizeReport.Status);
            return false;
        }

        result = CreateFromNormalizedQuaternion(normalized);
        if (!IsFiniteMatrix(result)) {
            result = default;
            report = MatrixReport(
                SpatialOperation.QuaternionMatrixConstruction,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        report = MatrixReport(
            SpatialOperation.QuaternionMatrixConstruction,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    private static SpatialOperationReport MatrixReport(
        SpatialOperation operation,
        SpatialOperationStatus status) {
        return new SpatialOperationReport(operation, status);
    }
}

public readonly partial record struct Matrix4x4 {
    /// <summary>
    /// Attempts to invert the matrix and reports exact singularity separately
    /// from non-finite data and unrepresentable output.
    /// </summary>
    public bool TryInvert(
        out Matrix4x4 inverse,
        out SpatialOperationReport report) {
        inverse = default;
        if (!IsFiniteMatrix(this)) {
            report = MatrixReport(
                SpatialOperation.MatrixInversion,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        if (TryInvertCore(out inverse)) {
            report = MatrixReport(
                SpatialOperation.MatrixInversion,
                SpatialOperationStatus.Succeeded);
            return true;
        }

        if (!TryGetDeterminant(this, out var determinant)) {
            report = MatrixReport(
                SpatialOperation.MatrixInversion,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        report = MatrixReport(
            SpatialOperation.MatrixInversion,
            determinant == 0
                ? SpatialOperationStatus.Singular
                : SpatialOperationStatus.NonFiniteOutput);
        return false;
    }

    /// <summary>
    /// Estimates the Frobenius condition number and reports inversion failures
    /// separately from an unrepresentable estimate.
    /// </summary>
    public bool TryEstimateConditionNumber(
        out double conditionNumber,
        out SpatialOperationReport report) {
        conditionNumber = double.PositiveInfinity;
        if (!TryInvert(out var inverse, out var inverseReport)) {
            report = MatrixReport(
                SpatialOperation.MatrixConditionEstimate,
                inverseReport.Status);
            return false;
        }

        conditionNumber = MatrixNumerics.FrobeniusNorm(this) *
            MatrixNumerics.FrobeniusNorm(inverse);
        if (!double.IsFinite(conditionNumber)) {
            conditionNumber = double.PositiveInfinity;
            report = MatrixReport(
                SpatialOperation.MatrixConditionEstimate,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        report = MatrixReport(
            SpatialOperation.MatrixConditionEstimate,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    private static bool IsFiniteMatrix(Matrix4x4 matrix) {
        return matrix.IsFinite;
    }

    private static bool TryGetDeterminant(
        Matrix4x4 matrix,
        out double determinant) {
        Span<double> source = stackalloc double[16];
        source[0] = matrix.M11;
        source[1] = matrix.M12;
        source[2] = matrix.M13;
        source[3] = matrix.M14;
        source[4] = matrix.M21;
        source[5] = matrix.M22;
        source[6] = matrix.M23;
        source[7] = matrix.M24;
        source[8] = matrix.M31;
        source[9] = matrix.M32;
        source[10] = matrix.M33;
        source[11] = matrix.M34;
        source[12] = matrix.M41;
        source[13] = matrix.M42;
        source[14] = matrix.M43;
        source[15] = matrix.M44;
        determinant = 1;
        for (var column = 0; column < 4; column++) {
            var pivotRow = column;
            var pivotMagnitude = Math.Abs(source[(column * 4) + column]);
            for (var row = column + 1; row < 4; row++) {
                var magnitude = Math.Abs(source[(row * 4) + column]);
                if (magnitude > pivotMagnitude) {
                    pivotMagnitude = magnitude;
                    pivotRow = row;
                }
            }

            if (!double.IsFinite(pivotMagnitude)) {
                determinant = double.NaN;
                return false;
            }

            if (pivotMagnitude == 0) {
                determinant = 0;
                return true;
            }

            if (pivotRow != column) {
                SwapDeterminantRows(source, pivotRow, column);
                determinant = -determinant;
            }

            var pivot = source[(column * 4) + column];
            determinant *= pivot;
            for (var row = column + 1; row < 4; row++) {
                var factor = source[(row * 4) + column] / pivot;
                for (var entry = column + 1; entry < 4; entry++) {
                    source[(row * 4) + entry] -=
                        factor * source[(column * 4) + entry];
                }
            }
        }

        return double.IsFinite(determinant);
    }

    private static void SwapDeterminantRows(
        Span<double> values,
        int first,
        int second) {
        for (var column = 0; column < 4; column++) {
            var firstIndex = (first * 4) + column;
            var secondIndex = (second * 4) + column;
            (values[firstIndex], values[secondIndex]) =
                (values[secondIndex], values[firstIndex]);
        }
    }

    private static SpatialOperationReport MatrixReport(
        SpatialOperation operation,
        SpatialOperationStatus status) {
        return new SpatialOperationReport(operation, status);
    }
}

public readonly partial record struct Quaternion {
    /// <summary>
    /// Creates a quaternion from an axis-angle pair and reports invalid or
    /// degenerate input.
    /// </summary>
    public static bool TryCreateFromAxisAngle(
        Vector3 axis,
        float radians,
        out Quaternion result,
        out SpatialOperationReport report) {
        result = Identity;
        if (!axis.IsFinite || !float.IsFinite(radians)) {
            report = QuaternionReport(
                SpatialOperation.QuaternionAxisAngleConstruction,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        if (!axis.TryNormalize(out var normal, out var normalizeReport)) {
            report = QuaternionReport(
                SpatialOperation.QuaternionAxisAngleConstruction,
                normalizeReport.Status);
            return false;
        }

        var halfAngle = radians / 2;
        var sine = MathF.Sin(halfAngle);
        result = new Quaternion(
            normal.X * sine,
            normal.Y * sine,
            normal.Z * sine,
            MathF.Cos(halfAngle));
        if (!result.IsFinite) {
            result = Identity;
            report = QuaternionReport(
                SpatialOperation.QuaternionAxisAngleConstruction,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        report = QuaternionReport(
            SpatialOperation.QuaternionAxisAngleConstruction,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Creates a quaternion from a finite, non-singular rotation matrix and
    /// reports why construction failed.
    /// </summary>
    /// <remarks>
    /// This overload does not validate orthonormality; the matrix must already
    /// be a rotation matrix. It checks finiteness and exact non-singularity so
    /// an undefined algorithm branch is not entered.
    /// </remarks>
    public static bool TryCreateFromRotationMatrix(
        Matrix3x3 matrix,
        out Quaternion result,
        out SpatialOperationReport report) {
        result = Identity;
        if (!IsFiniteMatrix(matrix)) {
            report = QuaternionReport(
                SpatialOperation.QuaternionRotationMatrixConstruction,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        double m11 = matrix.M11;
        double m12 = matrix.M12;
        double m13 = matrix.M13;
        double m21 = matrix.M21;
        double m22 = matrix.M22;
        double m23 = matrix.M23;
        double m31 = matrix.M31;
        double m32 = matrix.M32;
        double m33 = matrix.M33;
        var determinant =
            (m11 * ((m22 * m33) - (m23 * m32))) -
            (m12 * ((m21 * m33) - (m23 * m31))) +
            (m13 * ((m21 * m32) - (m22 * m31)));
        if (!double.IsFinite(determinant)) {
            report = QuaternionReport(
                SpatialOperation.QuaternionRotationMatrixConstruction,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        if (determinant == 0) {
            report = QuaternionReport(
                SpatialOperation.QuaternionRotationMatrixConstruction,
                SpatialOperationStatus.Singular);
            return false;
        }

        var trace = m11 + m22 + m33;
        double x;
        double y;
        double z;
        double w;
        if (trace > 0) {
            var scale = Math.Sqrt(trace + 1.0) * 2;
            w = 0.25 * scale;
            x = (m32 - m23) / scale;
            y = (m13 - m31) / scale;
            z = (m21 - m12) / scale;
        } else if (m11 > m22 && m11 > m33) {
            var scale = Math.Sqrt(1.0 + m11 - m22 - m33) * 2;
            w = (m32 - m23) / scale;
            x = 0.25 * scale;
            y = (m12 + m21) / scale;
            z = (m13 + m31) / scale;
        } else if (m22 > m33) {
            var scale = Math.Sqrt(1.0 + m22 - m11 - m33) * 2;
            w = (m13 - m31) / scale;
            x = (m12 + m21) / scale;
            y = 0.25 * scale;
            z = (m23 + m32) / scale;
        } else {
            var scale = Math.Sqrt(1.0 + m33 - m11 - m22) * 2;
            w = (m21 - m12) / scale;
            x = (m13 + m31) / scale;
            y = (m23 + m32) / scale;
            z = 0.25 * scale;
        }

        var lengthSquared = (x * x) + (y * y) + (z * z) + (w * w);
        if (!double.IsFinite(lengthSquared) || !(lengthSquared > 0)) {
            report = QuaternionReport(
                SpatialOperation.QuaternionRotationMatrixConstruction,
                double.IsFinite(lengthSquared)
                    ? SpatialOperationStatus.NumericalFailure
                    : SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        var inverseLength = 1 / Math.Sqrt(lengthSquared);
        var candidate = new Quaternion(
            (float)(x * inverseLength),
            (float)(y * inverseLength),
            (float)(z * inverseLength),
            (float)(w * inverseLength));
        if (!candidate.IsFinite) {
            report = QuaternionReport(
                SpatialOperation.QuaternionRotationMatrixConstruction,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        result = candidate;
        report = QuaternionReport(
            SpatialOperation.QuaternionRotationMatrixConstruction,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    /// <summary>Attempts to normalize the quaternion and reports why normalization failed.</summary>
    public bool TryNormalize(
        out Quaternion result,
        out SpatialOperationReport report) {
        result = Identity;
        if (!IsFinite) {
            report = QuaternionReport(
                SpatialOperation.QuaternionNormalization,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        double x = X;
        double y = Y;
        double z = Z;
        double w = W;
        var length = Math.Sqrt(
            (x * x) + (y * y) + (z * z) + (w * w));
        if (length == 0) {
            report = QuaternionReport(
                SpatialOperation.QuaternionNormalization,
                SpatialOperationStatus.DegenerateInput);
            return false;
        }

        var candidate = new Quaternion(
            (float)(x / length),
            (float)(y / length),
            (float)(z / length),
            (float)(w / length));
        if (!candidate.IsFinite) {
            report = QuaternionReport(
                SpatialOperation.QuaternionNormalization,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        result = candidate;
        report = QuaternionReport(
            SpatialOperation.QuaternionNormalization,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    /// <summary>Attempts to invert the quaternion without requesting a report.</summary>
    public bool TryInvert(out Quaternion result) {
        return TryInvert(out result, out _);
    }

    /// <summary>
    /// Attempts to invert the quaternion and reports zero/non-finite input
    /// separately from unrepresentable output.
    /// </summary>
    public bool TryInvert(
        out Quaternion result,
        out SpatialOperationReport report) {
        result = Identity;
        if (!IsFinite) {
            report = QuaternionReport(
                SpatialOperation.QuaternionInversion,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        double x = X;
        double y = Y;
        double z = Z;
        double w = W;
        var lengthSquared = (x * x) + (y * y) + (z * z) + (w * w);
        if (lengthSquared == 0) {
            report = QuaternionReport(
                SpatialOperation.QuaternionInversion,
                SpatialOperationStatus.DegenerateInput);
            return false;
        }

        var inverseLengthSquared = 1 / lengthSquared;
        var candidate = new Quaternion(
            (float)(-x * inverseLengthSquared),
            (float)(-y * inverseLengthSquared),
            (float)(-z * inverseLengthSquared),
            (float)(w * inverseLengthSquared));
        if (!candidate.IsFinite) {
            report = QuaternionReport(
                SpatialOperation.QuaternionInversion,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        result = candidate;
        report = QuaternionReport(
            SpatialOperation.QuaternionInversion,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    /// <summary>Attempts to rotate a vector by this quaternion.</summary>
    public bool TryRotate(Vector3 value, out Vector3 result) {
        return TryRotate(value, out result, out _);
    }

    /// <summary>
    /// Attempts to rotate a vector by this quaternion and reports non-finite or
    /// degenerate input and unrepresentable output.
    /// </summary>
    /// <remarks>
    /// A zero quaternion is rejected because it does not define a rotation.
    /// The quaternion is normalized first, but the input instance itself is not
    /// modified.
    /// </remarks>
    public bool TryRotate(
        Vector3 value,
        out Vector3 result,
        out SpatialOperationReport report) {
        result = default;
        if (!IsFinite || !value.IsFinite) {
            report = QuaternionReport(
                SpatialOperation.QuaternionRotation,
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        if (!TryNormalize(out var normalized, out var normalizeReport)) {
            report = QuaternionReport(
                SpatialOperation.QuaternionRotation,
                normalizeReport.Status);
            return false;
        }

        double x = normalized.X;
        double y = normalized.Y;
        double z = normalized.Z;
        double w = normalized.W;
        double vx = value.X;
        double vy = value.Y;
        double vz = value.Z;
        var crossX = (y * vz) - (z * vy);
        var crossY = (z * vx) - (x * vz);
        var crossZ = (x * vy) - (y * vx);
        var twiceCrossX = 2 * crossX;
        var twiceCrossY = 2 * crossY;
        var twiceCrossZ = 2 * crossZ;
        var rotatedX = vx + (w * twiceCrossX) +
            ((y * twiceCrossZ) - (z * twiceCrossY));
        var rotatedY = vy + (w * twiceCrossY) +
            ((z * twiceCrossX) - (x * twiceCrossZ));
        var rotatedZ = vz + (w * twiceCrossZ) +
            ((x * twiceCrossY) - (y * twiceCrossX));
        if (!MatrixNumerics.IsRepresentableAsFloat(rotatedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(rotatedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(rotatedZ)) {
            report = QuaternionReport(
                SpatialOperation.QuaternionRotation,
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        result = new Vector3(
            (float)rotatedX,
            (float)rotatedY,
            (float)rotatedZ);
        report = QuaternionReport(
            SpatialOperation.QuaternionRotation,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    private static bool IsFiniteMatrix(Matrix3x3 matrix) {
        return float.IsFinite(matrix.M11) &&
            float.IsFinite(matrix.M12) &&
            float.IsFinite(matrix.M13) &&
            float.IsFinite(matrix.M21) &&
            float.IsFinite(matrix.M22) &&
            float.IsFinite(matrix.M23) &&
            float.IsFinite(matrix.M31) &&
            float.IsFinite(matrix.M32) &&
            float.IsFinite(matrix.M33);
    }

    private static SpatialOperationReport QuaternionReport(
        SpatialOperation operation,
        SpatialOperationStatus status) {
        return new SpatialOperationReport(operation, status);
    }
}
