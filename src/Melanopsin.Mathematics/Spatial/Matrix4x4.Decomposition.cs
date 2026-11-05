namespace Melanopsin.Mathematics;

public readonly partial record struct Matrix4x4 {
    public bool IsFinite =>
        float.IsFinite(M11) &&
        float.IsFinite(M12) &&
        float.IsFinite(M13) &&
        float.IsFinite(M14) &&
        float.IsFinite(M21) &&
        float.IsFinite(M22) &&
        float.IsFinite(M23) &&
        float.IsFinite(M24) &&
        float.IsFinite(M31) &&
        float.IsFinite(M32) &&
        float.IsFinite(M33) &&
        float.IsFinite(M34) &&
        float.IsFinite(M41) &&
        float.IsFinite(M42) &&
        float.IsFinite(M43) &&
        float.IsFinite(M44);

    /// <summary>
    /// Tests whether the matrix is affine using the default spatial tolerance.
    /// </summary>
    public bool IsAffine() {
        return IsAffine(ScalarMath.DefaultSingleTolerance);
    }

    /// <summary>
    /// Tests whether the matrix is affine after normalization by <c>M44</c>.
    /// </summary>
    /// <param name="relativeTolerance">
    /// Maximum allowed <c>max(|M41|, |M42|, |M43|) / |M44|</c>. Must be finite
    /// and non-negative. Zero requires an exactly affine normalized matrix.
    /// </param>
    /// <remarks>
    /// This is a structural classification, not a near-singularity threshold.
    /// The matrix linear part may still be singular or ill-conditioned.
    /// </remarks>
    public bool IsAffine(float relativeTolerance) {
        if (!float.IsFinite(relativeTolerance) || relativeTolerance < 0) {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        }

        if (!float.IsFinite(M41) ||
            !float.IsFinite(M42) ||
            !float.IsFinite(M43) ||
            !float.IsFinite(M44) ||
            M44 == 0) {
            return false;
        }

        var inverseM44 = 1d / M44;
        return double.IsFinite(inverseM44) &&
            Math.Abs(M41 * inverseM44) <= relativeTolerance &&
            Math.Abs(M42 * inverseM44) <= relativeTolerance &&
            Math.Abs(M43 * inverseM44) <= relativeTolerance;
    }

    /// <summary>
    /// Attempts to decompose an affine homogeneous transform into translation,
    /// rotation, and axis scale.
    /// </summary>
    /// <remarks>
    /// The reconstruction convention is translation times rotation times
    /// diagonal scale, applied to column vectors. A negative determinant is
    /// represented by negating the X scale; the returned quaternion remains a
    /// proper right-handed rotation. The matrix must be affine within the
    /// default spatial tolerance and its three upper-left column lengths must
    /// be non-zero. Shear is not represented and makes decomposition fail.
    /// </remarks>
    public bool TryDecomposeAffine(
        out Vector3 translation,
        out Quaternion rotation,
        out Vector3 scale) {
        return TryDecomposeAffine(
            ScalarMath.DefaultSingleTolerance,
            out translation,
            out rotation,
            out scale,
            out _);
    }

    /// <summary>
    /// Attempts to decompose an affine homogeneous transform using an explicit
    /// relative tolerance.
    /// </summary>
    /// <remarks>
    /// The fourth-row residual and upper-left orthogonality are checked against
    /// <paramref name="relativeTolerance"/>. The tolerance affects only matrix
    /// structure classification; no determinant or scale magnitude threshold
    /// is applied.
    /// </remarks>
    public bool TryDecomposeAffine(
        float relativeTolerance,
        out Vector3 translation,
        out Quaternion rotation,
        out Vector3 scale) {
        return TryDecomposeAffine(
            relativeTolerance,
            out translation,
            out rotation,
            out scale,
            out _);
    }
}
