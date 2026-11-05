// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public readonly partial record struct Matrix4x4 {
    /// <summary>
    /// Attempts to decompose an affine homogeneous transform using the default
    /// relative tolerance and reports the failing stage.
    /// </summary>
    public bool TryDecomposeAffine(
        out Vector3 translation,
        out Quaternion rotation,
        out Vector3 scale,
        out SpatialOperationReport report) {
        return TryDecomposeAffine(
            ScalarMath.DefaultSingleTolerance,
            out translation,
            out rotation,
            out scale,
            out report);
    }

    /// <summary>
    /// Attempts to decompose an affine homogeneous transform using an explicit
    /// relative tolerance and reports whether the input was non-finite,
    /// non-affine, singular, or contained shear.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reconstruction convention is
    /// <c>translation * rotation * diagonalScale</c> for column vectors. A
    /// negative determinant is represented by negating the X scale, preserving
    /// a proper right-handed rotation.
    /// </para>
    /// <para>
    /// <paramref name="relativeTolerance"/> classifies the fourth-row residual
    /// and normalized upper-left orthogonality only. It is not a
    /// near-singularity threshold. A singular result means an exactly zero scale
    /// or determinant in the working precision.
    /// </para>
    /// </remarks>
    public bool TryDecomposeAffine(
        float relativeTolerance,
        out Vector3 translation,
        out Quaternion rotation,
        out Vector3 scale,
        out SpatialOperationReport report) {
        if (!float.IsFinite(relativeTolerance) || relativeTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }

        translation = default;
        rotation = Quaternion.Identity;
        scale = default;
        if (!IsFinite) {
            report = DecompositionReport(
                SpatialOperationStatus.NonFiniteInput);
            return false;
        }

        if (!float.IsFinite(M44) || M44 == 0 || !IsAffine(relativeTolerance)) {
            report = DecompositionReport(SpatialOperationStatus.NotAffine);
            return false;
        }

        var inverseM44 = 1d / M44;
        double m11 = M11 * inverseM44;
        double m12 = M12 * inverseM44;
        double m13 = M13 * inverseM44;
        double m21 = M21 * inverseM44;
        double m22 = M22 * inverseM44;
        double m23 = M23 * inverseM44;
        double m31 = M31 * inverseM44;
        double m32 = M32 * inverseM44;
        double m33 = M33 * inverseM44;
        var scaleX = Math.Sqrt((m11 * m11) + (m21 * m21) + (m31 * m31));
        var scaleY = Math.Sqrt((m12 * m12) + (m22 * m22) + (m32 * m32));
        var scaleZ = Math.Sqrt((m13 * m13) + (m23 * m23) + (m33 * m33));
        var determinant =
            (m11 * ((m22 * m33) - (m23 * m32))) -
            (m12 * ((m21 * m33) - (m23 * m31))) +
            (m13 * ((m21 * m32) - (m22 * m31)));
        if (!double.IsFinite(determinant) ||
            !double.IsFinite(scaleX) ||
            !double.IsFinite(scaleY) ||
            !double.IsFinite(scaleZ)) {
            report = DecompositionReport(
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        if (determinant == 0 ||
            !(scaleX > 0) ||
            !(scaleY > 0) ||
            !(scaleZ > 0)) {
            report = DecompositionReport(SpatialOperationStatus.Singular);
            return false;
        }

        if (determinant < 0) {
            scaleX = -scaleX;
        }

        var r11 = m11 / scaleX;
        var r21 = m21 / scaleX;
        var r31 = m31 / scaleX;
        var r12 = m12 / scaleY;
        var r22 = m22 / scaleY;
        var r32 = m32 / scaleY;
        var r13 = m13 / scaleZ;
        var r23 = m23 / scaleZ;
        var r33 = m33 / scaleZ;
        var orthogonalityError = Math.Max(
            Math.Abs((r11 * r11) + (r21 * r21) + (r31 * r31) - 1),
            Math.Max(
                Math.Abs((r12 * r12) + (r22 * r22) + (r32 * r32) - 1),
                Math.Max(
                    Math.Abs((r13 * r13) + (r23 * r23) + (r33 * r33) - 1),
                    Math.Max(
                        Math.Abs((r11 * r12) + (r21 * r22) + (r31 * r32)),
                        Math.Max(
                            Math.Abs((r11 * r13) + (r21 * r23) + (r31 * r33)),
                            Math.Abs((r12 * r13) + (r22 * r23) + (r32 * r33)))))));
        if (!double.IsFinite(orthogonalityError)) {
            report = DecompositionReport(
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        if (orthogonalityError > relativeTolerance) {
            report = DecompositionReport(SpatialOperationStatus.Shear);
            return false;
        }

        var translationX = M14 * inverseM44;
        var translationY = M24 * inverseM44;
        var translationZ = M34 * inverseM44;
        if (!MatrixNumerics.IsRepresentableAsFloat(translationX) ||
            !MatrixNumerics.IsRepresentableAsFloat(translationY) ||
            !MatrixNumerics.IsRepresentableAsFloat(translationZ) ||
            !MatrixNumerics.IsRepresentableAsFloat(scaleX) ||
            !MatrixNumerics.IsRepresentableAsFloat(scaleY) ||
            !MatrixNumerics.IsRepresentableAsFloat(scaleZ)) {
            report = DecompositionReport(
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        var rotationMatrix = new Matrix3x3(
            (float)r11, (float)r12, (float)r13,
            (float)r21, (float)r22, (float)r23,
            (float)r31, (float)r32, (float)r33);
        if (!Quaternion.TryCreateFromRotationMatrix(
                rotationMatrix,
                out rotation,
                out var rotationReport)) {
            rotation = Quaternion.Identity;
            report = DecompositionReport(rotationReport.Status);
            return false;
        }

        translation = new Vector3(
            (float)translationX,
            (float)translationY,
            (float)translationZ);
        scale = new Vector3((float)scaleX, (float)scaleY, (float)scaleZ);
        if (!translation.IsFinite || !rotation.IsFinite || !scale.IsFinite) {
            translation = default;
            rotation = Quaternion.Identity;
            scale = default;
            report = DecompositionReport(
                SpatialOperationStatus.NonFiniteOutput);
            return false;
        }

        report = DecompositionReport(SpatialOperationStatus.Succeeded);
        return true;
    }

    private static SpatialOperationReport DecompositionReport(
        SpatialOperationStatus status) {
        return new SpatialOperationReport(
            SpatialOperation.AffineDecomposition,
            status);
    }
}
