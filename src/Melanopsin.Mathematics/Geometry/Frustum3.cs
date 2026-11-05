namespace Melanopsin.Mathematics;

/// <summary>
/// The depth convention used by a homogeneous clip volume.
/// </summary>
public enum ClipDepthRange {
    /// <summary>Clip-space Z is in <c>[0, w]</c>, as in Direct3D and Vulkan.</summary>
    ZeroToOne,

    /// <summary>Clip-space Z is in <c>[-w, w]</c>, as in OpenGL.</summary>
    NegativeOneToOne,
}

/// <summary>
/// A convex six-plane view frustum.
/// </summary>
/// <remarks>
/// All planes are inward-facing: their positive half-space is inside the
/// frustum. The constructor cannot infer orientation from six arbitrary
/// planes, so callers constructing a frustum manually must preserve that
/// contract. <see cref="TryFromViewProjection(Matrix4x4, ClipDepthRange, out Frustum3)"/> extracts correctly oriented
/// planes from a homogeneous view-projection matrix.
/// </remarks>
public readonly partial record struct Frustum3 {
    public Frustum3(
        Plane3 left,
        Plane3 right,
        Plane3 bottom,
        Plane3 top,
        Plane3 near,
        Plane3 far) {
        Left = left;
        Right = right;
        Bottom = bottom;
        Top = top;
        Near = near;
        Far = far;
    }

    public Plane3 Left { get; }

    public Plane3 Right { get; }

    public Plane3 Bottom { get; }

    public Plane3 Top { get; }

    public Plane3 Near { get; }

    public Plane3 Far { get; }

    /// <summary>
    /// Extracts a frustum from a row-major, column-vector view-projection
    /// matrix.
    /// </summary>
    /// <remarks>
    /// The extraction uses the standard row combinations
    /// <c>w +/- x</c>, <c>w +/- y</c>, and the appropriate near/far
    /// combinations for <paramref name="depthRange"/>. It supports arbitrary
    /// finite homogeneous clip conventions but assumes that the input matrix
    /// uses this library's column-vector convention. An undefined
    /// <paramref name="depthRange"/> value is a programming error and throws
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    public static bool TryFromViewProjection(
        Matrix4x4 viewProjection,
        ClipDepthRange depthRange,
        out Frustum3 frustum) {
        return TryFromViewProjection(
            viewProjection,
            depthRange,
            out frustum,
            out _);
    }

    /// <summary>
    /// Attempts to extract a frustum and reports non-finite matrix data or a
    /// degenerate clip plane.
    /// </summary>
    public static bool TryFromViewProjection(
        Matrix4x4 viewProjection,
        ClipDepthRange depthRange,
        out Frustum3 frustum,
        out GeometryOperationReport report) {
        if (depthRange is not ClipDepthRange.ZeroToOne and
            not ClipDepthRange.NegativeOneToOne) {
            throw new ArgumentOutOfRangeException(nameof(depthRange));
        }

        if (!IsFinite(viewProjection)) {
            frustum = default;
            report = new GeometryOperationReport(
                GeometryOperation.FrustumConstruction,
                GeometryStatus.NonFiniteInput);
            return false;
        }

        var left = AddRows(viewProjection.Row4, viewProjection.Row1);
        var right = SubtractRows(viewProjection.Row4, viewProjection.Row1);
        var bottom = AddRows(viewProjection.Row4, viewProjection.Row2);
        var top = SubtractRows(viewProjection.Row4, viewProjection.Row2);
        var near = depthRange == ClipDepthRange.ZeroToOne
            ? viewProjection.Row3
            : AddRows(viewProjection.Row4, viewProjection.Row3);
        var far = SubtractRows(viewProjection.Row4, viewProjection.Row3);
        if (!TryCreatePlane(left, out var leftPlane) ||
            !TryCreatePlane(right, out var rightPlane) ||
            !TryCreatePlane(bottom, out var bottomPlane) ||
            !TryCreatePlane(top, out var topPlane) ||
            !TryCreatePlane(near, out var nearPlane) ||
            !TryCreatePlane(far, out var farPlane)) {
            frustum = default;
            report = new GeometryOperationReport(
                GeometryOperation.FrustumConstruction,
                GetPlaneFailureStatus(
                    left,
                    right,
                    bottom,
                    top,
                    near,
                    far));
            return false;
        }

        frustum = new Frustum3(
            leftPlane,
            rightPlane,
            bottomPlane,
            topPlane,
            nearPlane,
            farPlane);
        report = new GeometryOperationReport(
            GeometryOperation.FrustumConstruction,
            GeometryStatus.Succeeded);
        return true;
    }

    public bool Contains(Vector3 point) {
        return IsInside(Left, point) &&
            IsInside(Right, point) &&
            IsInside(Bottom, point) &&
            IsInside(Top, point) &&
            IsInside(Near, point) &&
            IsInside(Far, point);
    }

    public bool Intersects(Aabb3 bounds) {
        return IntersectsPlane(Left, bounds) &&
            IntersectsPlane(Right, bounds) &&
            IntersectsPlane(Bottom, bounds) &&
            IntersectsPlane(Top, bounds) &&
            IntersectsPlane(Near, bounds) &&
            IntersectsPlane(Far, bounds);
    }

    public bool Intersects(Sphere3 sphere) {
        return IntersectsPlane(Left, sphere) &&
            IntersectsPlane(Right, sphere) &&
            IntersectsPlane(Bottom, sphere) &&
            IntersectsPlane(Top, sphere) &&
            IntersectsPlane(Near, sphere) &&
            IntersectsPlane(Far, sphere);
    }

    private static bool IntersectsPlane(Plane3 plane, Aabb3 bounds) {
        var distance = plane.SignedDistanceCore(bounds.Center);
        var radius =
            (Math.Abs((double)plane.Normal.X) * bounds.Extents.X) +
            (Math.Abs((double)plane.Normal.Y) * bounds.Extents.Y) +
            (Math.Abs((double)plane.Normal.Z) * bounds.Extents.Z);
        return distance + radius >= 0;
    }

    private static bool IntersectsPlane(Plane3 plane, Sphere3 sphere) {
        return plane.SignedDistanceCore(sphere.Center) + sphere.Radius >= 0;
    }

    private static bool IsInside(Plane3 plane, Vector3 point) {
        return plane.SignedDistanceCore(point) >= 0;
    }

    private static bool TryCreatePlane(Vector4 coefficients, out Plane3 plane) {
        if (!coefficients.IsFinite) {
            plane = default;
            return false;
        }

        double x = coefficients.X;
        double y = coefficients.Y;
        double z = coefficients.Z;
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        if (length == 0) {
            plane = default;
            return false;
        }

        var normalized = new Vector3(
            (float)(x / length),
            (float)(y / length),
            (float)(z / length));
        var distance = coefficients.W / length;
        if (!MatrixNumerics.IsRepresentableAsFloat(x / length) ||
            !MatrixNumerics.IsRepresentableAsFloat(y / length) ||
            !MatrixNumerics.IsRepresentableAsFloat(z / length) ||
            !MatrixNumerics.IsRepresentableAsFloat(distance)) {
            plane = default;
            return false;
        }

        plane = new Plane3(normalized, (float)distance);
        return true;
    }

    private static GeometryStatus GetPlaneFailureStatus(
        Vector4 left,
        Vector4 right,
        Vector4 bottom,
        Vector4 top,
        Vector4 near,
        Vector4 far) {
        if (!left.IsFinite ||
            !right.IsFinite ||
            !bottom.IsFinite ||
            !top.IsFinite ||
            !near.IsFinite ||
            !far.IsFinite) {
            return GeometryStatus.NonFiniteOutput;
        }

        if (HasZeroNormal(left) ||
            HasZeroNormal(right) ||
            HasZeroNormal(bottom) ||
            HasZeroNormal(top) ||
            HasZeroNormal(near) ||
            HasZeroNormal(far)) {
            return GeometryStatus.DegenerateGeometry;
        }

        return GeometryStatus.NonFiniteOutput;
    }

    private static bool HasZeroNormal(Vector4 coefficients) {
        return coefficients.X == 0 &&
            coefficients.Y == 0 &&
            coefficients.Z == 0;
    }

    private static bool IsFinite(Matrix4x4 matrix) {
        return float.IsFinite(matrix.M11) &&
            float.IsFinite(matrix.M12) &&
            float.IsFinite(matrix.M13) &&
            float.IsFinite(matrix.M14) &&
            float.IsFinite(matrix.M21) &&
            float.IsFinite(matrix.M22) &&
            float.IsFinite(matrix.M23) &&
            float.IsFinite(matrix.M24) &&
            float.IsFinite(matrix.M31) &&
            float.IsFinite(matrix.M32) &&
            float.IsFinite(matrix.M33) &&
            float.IsFinite(matrix.M34) &&
            float.IsFinite(matrix.M41) &&
            float.IsFinite(matrix.M42) &&
            float.IsFinite(matrix.M43) &&
            float.IsFinite(matrix.M44);
    }

    private static Vector4 AddRows(Vector4 first, Vector4 second) {
        return new Vector4(
            first.X + second.X,
            first.Y + second.Y,
            first.Z + second.Z,
            first.W + second.W);
    }

    private static Vector4 SubtractRows(Vector4 first, Vector4 second) {
        return new Vector4(
            first.X - second.X,
            first.Y - second.Y,
            first.Z - second.Z,
            first.W - second.W);
    }
}
