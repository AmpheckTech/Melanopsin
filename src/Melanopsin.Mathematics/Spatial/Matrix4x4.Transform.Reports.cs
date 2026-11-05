namespace Melanopsin.Mathematics;

/// <summary>Identifies a homogeneous matrix transformation.</summary>
public enum MatrixTransformOperation {
    /// <summary>Transformation of a three-dimensional point through the full homogeneous matrix.</summary>
    Point,

    /// <summary>
    /// Transformation of a point-at-infinity direction through a matrix whose
    /// projective row entries are zero.
    /// </summary>
    Direction,
}

/// <summary>The termination status of a homogeneous matrix transformation.</summary>
public enum MatrixTransformStatus {
    /// <summary>The transformation produced a finite representable result.</summary>
    Succeeded,

    /// <summary>A point or direction component is not finite.</summary>
    NonFiniteInput,

    /// <summary>At least one matrix coefficient is not finite.</summary>
    NonFiniteMatrix,

    /// <summary>A direction is exactly zero in the working precision.</summary>
    DegenerateDirection,

    /// <summary>
    /// The transformed homogeneous W component is exactly zero, so the point
    /// maps to or comes from the plane at infinity and has no finite Cartesian
    /// representative.
    /// </summary>
    ProjectiveSingularity,

    /// <summary>
    /// A direction was requested from a matrix with a non-zero M41, M42, or M43
    /// entry. Such a matrix does not preserve the point-at-infinity plane and
    /// therefore has no point-independent direction mapping.
    /// </summary>
    NotAffine,

    /// <summary>The homogeneous W component overflowed or otherwise became non-finite.</summary>
    NonFiniteW,

    /// <summary>The projected components overflowed or are otherwise not representable as single precision.</summary>
    NonFiniteOutput,
}

/// <summary>
/// A diagnostic report for homogeneous point and direction transformations.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MatrixTransformOperation.Point"/> uses the complete homogeneous
/// equation <c>(x', y', z', w') = M (x, y, z, 1)</c> followed by the
/// perspective divide. This formula applies unchanged to affine and projective
/// matrices. A zero or non-finite <c>w'</c> is reported explicitly rather than
/// silently returning unprojected coordinates.
/// </para>
/// <para>
/// <see cref="MatrixTransformOperation.Direction"/> applies only the upper-left
/// three-by-three linear part and requires exactly <c>M41 = M42 = M43 = 0</c>.
/// For a direction <c>(x, y, z, 0)</c>, any finite <c>M44</c> cancels in the
/// homogeneous scale and is therefore irrelevant, including <c>M44 = 0</c>.
/// A matrix with a non-zero projective row does not preserve the plane at
/// infinity; transforming two points and subtracting them is required for a
/// local projected direction. Near-affine callers opt into an explicit relative
/// tolerance through
/// <see cref="Matrix4x4.TryTransformAffineDirection(Vector3, float, out Vector3)"/>.
/// </para>
/// </remarks>
public readonly record struct MatrixTransformReport {
    internal MatrixTransformReport(
        MatrixTransformOperation operation,
        MatrixTransformStatus status) {
        Operation = operation;
        Status = status;
    }

    /// <summary>Gets the requested transformation kind.</summary>
    public MatrixTransformOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public MatrixTransformStatus Status { get; }

    /// <summary>Gets whether the transformation produced a usable result.</summary>
    public bool Succeeded => Status == MatrixTransformStatus.Succeeded;
}

public readonly partial record struct Matrix4x4 {
    /// <summary>Attempts to construct an affine rotation matrix from a quaternion.</summary>
    public static bool TryCreateFromQuaternion(
        Quaternion quaternion,
        out Matrix4x4 result) {
        return TryCreateFromQuaternion(quaternion, out result, out _);
    }

    /// <summary>
    /// Attempts to construct an affine rotation matrix from a quaternion and
    /// reports non-finite or zero quaternion input.
    /// </summary>
    /// <remarks>
    /// The resulting fourth row and translation column are exactly those of an
    /// affine rotation. A zero quaternion is rejected because it carries no
    /// rotation.
    /// </remarks>
    public static bool TryCreateFromQuaternion(
        Quaternion quaternion,
        out Matrix4x4 result,
        out SpatialOperationReport report) {
        result = default;
        if (!Matrix3x3.TryCreateFromQuaternion(
                quaternion,
                out var rotation,
                out var constructionReport)) {
            report = new SpatialOperationReport(
                SpatialOperation.QuaternionMatrixConstruction,
                constructionReport.Status);
            return false;
        }

        result = FromRotation(rotation);
        report = new SpatialOperationReport(
            SpatialOperation.QuaternionMatrixConstruction,
            SpatialOperationStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts a full homogeneous point transformation and reports why a
    /// perspective divide could not produce finite Cartesian coordinates.
    /// </summary>
    /// <remarks>
    /// The calculation is performed in double precision before conversion back
    /// to <see cref="float"/>. This avoids treating an intermediate float
    /// overflow as a mathematical overflow. The result is accepted only when
    /// all three projected components are finite and within the finite
    /// <see cref="float"/> range.
    /// </remarks>
    public bool TryTransformPoint(
        Vector3 value,
        out Vector3 result,
        out MatrixTransformReport report) {
        result = default;
        if (!value.IsFinite) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Point,
                MatrixTransformStatus.NonFiniteInput);
            return false;
        }

        if (!IsFinite) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Point,
                MatrixTransformStatus.NonFiniteMatrix);
            return false;
        }

        double x = value.X;
        double y = value.Y;
        double z = value.Z;
        var transformedX =
            (M11 * x) + (M12 * y) + (M13 * z) + M14;
        var transformedY =
            (M21 * x) + (M22 * y) + (M23 * z) + M24;
        var transformedZ =
            (M31 * x) + (M32 * y) + (M33 * z) + M34;
        var transformedW =
            (M41 * x) + (M42 * y) + (M43 * z) + M44;
        if (double.IsNaN(transformedW) || double.IsInfinity(transformedW)) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Point,
                MatrixTransformStatus.NonFiniteW);
            return false;
        }

        if (transformedW == 0) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Point,
                MatrixTransformStatus.ProjectiveSingularity);
            return false;
        }

        var projectedX = transformedX / transformedW;
        var projectedY = transformedY / transformedW;
        var projectedZ = transformedZ / transformedW;
        if (!MatrixNumerics.IsRepresentableAsFloat(projectedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(projectedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(projectedZ)) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Point,
                MatrixTransformStatus.NonFiniteOutput);
            return false;
        }

        result = new Vector3(
            (float)projectedX,
            (float)projectedY,
            (float)projectedZ);
        report = new MatrixTransformReport(
            MatrixTransformOperation.Point,
            MatrixTransformStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts a direction transformation through a matrix classified as
    /// affine within an explicit relative tolerance and reports the failing
    /// stage.
    /// </summary>
    /// <remarks>
    /// The fourth-row residual is measured after normalization by M44. Output
    /// is also normalized by M44 so the method accepts an affine matrix written
    /// with any non-zero homogeneous scale. This structural tolerance is not a
    /// singularity threshold.
    /// </remarks>
    public bool TryTransformAffineDirection(
        Vector3 value,
        float relativeTolerance,
        out Vector3 result,
        out MatrixTransformReport report) {
        if (!float.IsFinite(relativeTolerance) || relativeTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }

        result = default;
        if (!value.IsFinite) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NonFiniteInput);
            return false;
        }

        if (!IsFinite) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NonFiniteMatrix);
            return false;
        }

        if (value.X == 0 && value.Y == 0 && value.Z == 0) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.DegenerateDirection);
            return false;
        }

        if (!IsAffine(relativeTolerance)) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NotAffine);
            return false;
        }

        var inverseM44 = 1d / M44;
        double x = value.X;
        double y = value.Y;
        double z = value.Z;
        var transformedX = ((M11 * x) + (M12 * y) + (M13 * z)) * inverseM44;
        var transformedY = ((M21 * x) + (M22 * y) + (M23 * z)) * inverseM44;
        var transformedZ = ((M31 * x) + (M32 * y) + (M33 * z)) * inverseM44;
        if (!MatrixNumerics.IsRepresentableAsFloat(transformedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(transformedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(transformedZ)) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NonFiniteOutput);
            return false;
        }

        result = new Vector3(
            (float)transformedX,
            (float)transformedY,
            (float)transformedZ);
        report = new MatrixTransformReport(
            MatrixTransformOperation.Direction,
            MatrixTransformStatus.Succeeded);
        return true;
    }

    /// <summary>
    /// Attempts an exact point-at-infinity direction transformation and reports
    /// non-finite data, zero direction, or a matrix with a projective row.
    /// </summary>
    /// <remarks>
    /// Translation is ignored. M44 is irrelevant to a point-at-infinity vector
    /// and is not used to rescale the result; in particular, M44 may be zero.
    /// Only M41, M42, and M43 must be exactly zero. Callers with approximately
    /// affine input that also need normalization by a non-zero M44 must choose a
    /// tolerance explicitly through
    /// <see cref="TryTransformAffineDirection(Vector3, float, out Vector3)"/>.
    /// </remarks>
    public bool TryTransformDirection(
        Vector3 value,
        out Vector3 result,
        out MatrixTransformReport report) {
        result = default;
        if (!value.IsFinite) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NonFiniteInput);
            return false;
        }

        if (!IsFinite) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NonFiniteMatrix);
            return false;
        }

        if (value.X == 0 && value.Y == 0 && value.Z == 0) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.DegenerateDirection);
            return false;
        }

        if (M41 != 0 || M42 != 0 || M43 != 0) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NotAffine);
            return false;
        }

        double x = value.X;
        double y = value.Y;
        double z = value.Z;
        var transformedX = (M11 * x) + (M12 * y) + (M13 * z);
        var transformedY = (M21 * x) + (M22 * y) + (M23 * z);
        var transformedZ = (M31 * x) + (M32 * y) + (M33 * z);
        if (!MatrixNumerics.IsRepresentableAsFloat(transformedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(transformedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(transformedZ)) {
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NonFiniteOutput);
            return false;
        }

        result = new Vector3(
            (float)transformedX,
            (float)transformedY,
            (float)transformedZ);
        if (!result.IsFinite) {
            result = default;
            report = new MatrixTransformReport(
                MatrixTransformOperation.Direction,
                MatrixTransformStatus.NonFiniteOutput);
            return false;
        }

        report = new MatrixTransformReport(
            MatrixTransformOperation.Direction,
            MatrixTransformStatus.Succeeded);
        return true;
    }
}
