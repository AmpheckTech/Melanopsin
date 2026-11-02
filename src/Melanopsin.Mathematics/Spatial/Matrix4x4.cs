using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A four-by-four matrix using row-major storage and column-vector
/// multiplication. Translation is stored in the fourth column.
/// </summary>
public readonly record struct Matrix4x4 :
    IMultiplyOperators<Matrix4x4, Matrix4x4, Matrix4x4>,
    IMultiplyOperators<Matrix4x4, float, Matrix4x4>,
    IDivisionOperators<Matrix4x4, float, Matrix4x4>,
    IMultiplicativeIdentity<Matrix4x4, Matrix4x4>,
    IEqualityOperators<Matrix4x4, Matrix4x4, bool> {
    public Matrix4x4(
        float m11,
        float m12,
        float m13,
        float m14,
        float m21,
        float m22,
        float m23,
        float m24,
        float m31,
        float m32,
        float m33,
        float m34,
        float m41,
        float m42,
        float m43,
        float m44) {
        M11 = m11;
        M12 = m12;
        M13 = m13;
        M14 = m14;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        M24 = m24;
        M31 = m31;
        M32 = m32;
        M33 = m33;
        M34 = m34;
        M41 = m41;
        M42 = m42;
        M43 = m43;
        M44 = m44;
    }

    public static Matrix4x4 Identity => new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    public static Matrix4x4 MultiplicativeIdentity => Identity;

    public float M11 { get; }

    public float M12 { get; }

    public float M13 { get; }

    public float M14 { get; }

    public float M21 { get; }

    public float M22 { get; }

    public float M23 { get; }

    public float M24 { get; }

    public float M31 { get; }

    public float M32 { get; }

    public float M33 { get; }

    public float M34 { get; }

    public float M41 { get; }

    public float M42 { get; }

    public float M43 { get; }

    public float M44 { get; }

    public Vector4 Row1 => new(M11, M12, M13, M14);

    public Vector4 Row2 => new(M21, M22, M23, M24);

    public Vector4 Row3 => new(M31, M32, M33, M34);

    public Vector4 Row4 => new(M41, M42, M43, M44);

    public Vector4 Column1 => new(M11, M21, M31, M41);

    public Vector4 Column2 => new(M12, M22, M32, M42);

    public Vector4 Column3 => new(M13, M23, M33, M43);

    public Vector4 Column4 => new(M14, M24, M34, M44);

    public static Matrix4x4 CreateTranslation(Vector3 translation) {
        return new Matrix4x4(
            1, 0, 0, translation.X,
            0, 1, 0, translation.Y,
            0, 0, 1, translation.Z,
            0, 0, 0, 1);
    }

    public static Matrix4x4 CreateScale(float scale) {
        return CreateScale(new Vector3(scale, scale, scale));
    }

    public static Matrix4x4 CreateScale(Vector3 scale) {
        return new Matrix4x4(
            scale.X, 0, 0, 0,
            0, scale.Y, 0, 0,
            0, 0, scale.Z, 0,
            0, 0, 0, 1);
    }

    public static Matrix4x4 CreateRotationX(float radians) {
        var rotation = Matrix3x3.CreateRotationX(radians);
        return FromRotation(rotation);
    }

    public static Matrix4x4 CreateRotationY(float radians) {
        var rotation = Matrix3x3.CreateRotationY(radians);
        return FromRotation(rotation);
    }

    public static Matrix4x4 CreateRotationZ(float radians) {
        var rotation = Matrix3x3.CreateRotationZ(radians);
        return FromRotation(rotation);
    }

    public static Matrix4x4 CreateFromAxisAngle(
        Vector3 axis,
        float radians) {
        return CreateFromQuaternion(Quaternion.CreateFromAxisAngle(axis, radians));
    }

    public static Matrix4x4 CreateFromQuaternion(Quaternion quaternion) {
        return FromRotation(Matrix3x3.CreateFromQuaternion(quaternion));
    }

    /// <summary>
    /// Creates a right-handed view matrix. Normalized device depth increases
    /// from zero at the near plane to one at the far plane.
    /// </summary>
    public static Matrix4x4 CreateLookAt(
        Vector3 position,
        Vector3 target,
        Vector3 up) {
        if (!position.IsFinite || !target.IsFinite || !up.IsFinite) {
            throw new ArgumentException("Look-at vectors must be finite.");
        }

        if (!(target - position).TryNormalize(out var forward)) {
            throw new ArgumentException(
                "The look-at position and target must be different.",
                nameof(target));
        }

        var backward = -forward;
        if (!Vector3.Cross(up, backward).TryNormalize(out var right)) {
            throw new ArgumentException(
                "The up vector must not be parallel to the view direction.",
                nameof(up));
        }

        var correctedUp = Vector3.Cross(backward, right);
        return new Matrix4x4(
            right.X, right.Y, right.Z, -Vector3.Dot(right, position),
            correctedUp.X, correctedUp.Y, correctedUp.Z, -Vector3.Dot(correctedUp, position),
            backward.X, backward.Y, backward.Z, -Vector3.Dot(backward, position),
            0, 0, 0, 1);
    }

    public static Matrix4x4 CreatePerspectiveFieldOfView(
        float fieldOfViewRadians,
        float aspectRatio,
        float nearPlane,
        float farPlane) {
        if (!float.IsFinite(fieldOfViewRadians) ||
            fieldOfViewRadians <= 0 ||
            fieldOfViewRadians >= MathF.PI) {
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewRadians));
        }

        if (!float.IsFinite(aspectRatio) || aspectRatio <= 0) {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio));
        }

        ValidateDepthRange(nearPlane, farPlane);
        var tangent = MathF.Tan(fieldOfViewRadians / 2);
        var verticalScale = 1 / tangent;
        var horizontalScale = verticalScale / aspectRatio;
        var depthScale = farPlane / (nearPlane - farPlane);
        var depthTranslation = (nearPlane * farPlane) / (nearPlane - farPlane);
        return new Matrix4x4(
            horizontalScale, 0, 0, 0,
            0, verticalScale, 0, 0,
            0, 0, depthScale, depthTranslation,
            0, 0, -1, 0);
    }

    public static Matrix4x4 CreateOrthographic(
        float width,
        float height,
        float nearPlane,
        float farPlane) {
        if (!float.IsFinite(width) || width <= 0) {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!float.IsFinite(height) || height <= 0) {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        ValidateDepthRange(nearPlane, farPlane);
        var depthScale = 1 / (nearPlane - farPlane);
        var depthTranslation = nearPlane / (nearPlane - farPlane);
        return new Matrix4x4(
            2 / width, 0, 0, 0,
            0, 2 / height, 0, 0,
            0, 0, depthScale, depthTranslation,
            0, 0, 0, 1);
    }

    public Vector4 Transform(Vector4 value) {
        return new Vector4(
            (M11 * value.X) + (M12 * value.Y) + (M13 * value.Z) + (M14 * value.W),
            (M21 * value.X) + (M22 * value.Y) + (M23 * value.Z) + (M24 * value.W),
            (M31 * value.X) + (M32 * value.Y) + (M33 * value.Z) + (M34 * value.W),
            (M41 * value.X) + (M42 * value.Y) + (M43 * value.Z) + (M44 * value.W));
    }

    public Vector3 TransformPoint(Vector3 value) {
        return new Vector3(
            (M11 * value.X) + (M12 * value.Y) + (M13 * value.Z) + M14,
            (M21 * value.X) + (M22 * value.Y) + (M23 * value.Z) + M24,
            (M31 * value.X) + (M32 * value.Y) + (M33 * value.Z) + M34);
    }

    public Vector3 TransformDirection(Vector3 value) {
        return new Vector3(
            (M11 * value.X) + (M12 * value.Y) + (M13 * value.Z),
            (M21 * value.X) + (M22 * value.Y) + (M23 * value.Z),
            (M31 * value.X) + (M32 * value.Y) + (M33 * value.Z));
    }

    public Matrix4x4 Transpose() {
        return new Matrix4x4(
            M11, M21, M31, M41,
            M12, M22, M32, M42,
            M13, M23, M33, M43,
            M14, M24, M34, M44);
    }

    public Matrix4x4 Invert() {
        if (!TryInvert(out var inverse)) {
            throw new InvalidOperationException("The matrix is singular.");
        }

        return inverse;
    }

    public bool TryInvert(out Matrix4x4 inverse) {
        Span<float> source = stackalloc float[16] {
            M11, M12, M13, M14,
            M21, M22, M23, M24,
            M31, M32, M33, M34,
            M41, M42, M43, M44,
        };
        Span<float> result = stackalloc float[16] {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1,
        };

        for (var column = 0; column < 4; column++) {
            var pivotRow = column;
            var pivotMagnitude = MathF.Abs(source[(column * 4) + column]);
            for (var row = column + 1; row < 4; row++) {
                var magnitude = MathF.Abs(source[(row * 4) + column]);
                if (magnitude > pivotMagnitude) {
                    pivotMagnitude = magnitude;
                    pivotRow = row;
                }
            }

            if (!float.IsFinite(pivotMagnitude) || pivotMagnitude <= ScalarMath.Epsilon) {
                inverse = default;
                return false;
            }

            if (pivotRow != column) {
                SwapRows(source, pivotRow, column);
                SwapRows(result, pivotRow, column);
            }

            var pivotIndex = (column * 4) + column;
            var inversePivot = 1 / source[pivotIndex];
            ScaleRow(source, column, inversePivot);
            ScaleRow(result, column, inversePivot);

            for (var row = 0; row < 4; row++) {
                if (row == column) {
                    continue;
                }

                var factor = source[(row * 4) + column];
                if (factor == 0) {
                    continue;
                }

                SubtractScaledRow(source, row, column, factor);
                SubtractScaledRow(result, row, column, factor);
            }
        }

        inverse = new Matrix4x4(
            result[0], result[1], result[2], result[3],
            result[4], result[5], result[6], result[7],
            result[8], result[9], result[10], result[11],
            result[12], result[13], result[14], result[15]);
        return true;
    }

    public static Matrix4x4 operator *(
        Matrix4x4 left,
        Matrix4x4 right) {
        return new Matrix4x4(
            DotRowColumn(left.Row1, right.Column1),
            DotRowColumn(left.Row1, right.Column2),
            DotRowColumn(left.Row1, right.Column3),
            DotRowColumn(left.Row1, right.Column4),
            DotRowColumn(left.Row2, right.Column1),
            DotRowColumn(left.Row2, right.Column2),
            DotRowColumn(left.Row2, right.Column3),
            DotRowColumn(left.Row2, right.Column4),
            DotRowColumn(left.Row3, right.Column1),
            DotRowColumn(left.Row3, right.Column2),
            DotRowColumn(left.Row3, right.Column3),
            DotRowColumn(left.Row3, right.Column4),
            DotRowColumn(left.Row4, right.Column1),
            DotRowColumn(left.Row4, right.Column2),
            DotRowColumn(left.Row4, right.Column3),
            DotRowColumn(left.Row4, right.Column4));
    }

    public static Vector4 operator *(Matrix4x4 matrix, Vector4 value) {
        return matrix.Transform(value);
    }

    public static Matrix4x4 operator *(Matrix4x4 matrix, float scale) {
        return new Matrix4x4(
            matrix.M11 * scale, matrix.M12 * scale, matrix.M13 * scale, matrix.M14 * scale,
            matrix.M21 * scale, matrix.M22 * scale, matrix.M23 * scale, matrix.M24 * scale,
            matrix.M31 * scale, matrix.M32 * scale, matrix.M33 * scale, matrix.M34 * scale,
            matrix.M41 * scale, matrix.M42 * scale, matrix.M43 * scale, matrix.M44 * scale);
    }

    public static Matrix4x4 operator *(float scale, Matrix4x4 matrix) {
        return matrix * scale;
    }

    public static Matrix4x4 operator /(Matrix4x4 matrix, float divisor) {
        return new Matrix4x4(
            matrix.M11 / divisor, matrix.M12 / divisor, matrix.M13 / divisor, matrix.M14 / divisor,
            matrix.M21 / divisor, matrix.M22 / divisor, matrix.M23 / divisor, matrix.M24 / divisor,
            matrix.M31 / divisor, matrix.M32 / divisor, matrix.M33 / divisor, matrix.M34 / divisor,
            matrix.M41 / divisor, matrix.M42 / divisor, matrix.M43 / divisor, matrix.M44 / divisor);
    }

    private static Matrix4x4 FromRotation(Matrix3x3 rotation) {
        return new Matrix4x4(
            rotation.M11, rotation.M12, rotation.M13, 0,
            rotation.M21, rotation.M22, rotation.M23, 0,
            rotation.M31, rotation.M32, rotation.M33, 0,
            0, 0, 0, 1);
    }

    private static float DotRowColumn(Vector4 row, Vector4 column) {
        return Vector4.Dot(row, column);
    }

    private static void SwapRows(Span<float> matrix, int first, int second) {
        for (var column = 0; column < 4; column++) {
            var firstIndex = (first * 4) + column;
            var secondIndex = (second * 4) + column;
            (matrix[firstIndex], matrix[secondIndex]) =
                (matrix[secondIndex], matrix[firstIndex]);
        }
    }

    private static void ScaleRow(Span<float> matrix, int row, float scale) {
        for (var column = 0; column < 4; column++) {
            matrix[(row * 4) + column] *= scale;
        }
    }

    private static void SubtractScaledRow(
        Span<float> matrix,
        int targetRow,
        int sourceRow,
        float scale) {
        for (var column = 0; column < 4; column++) {
            matrix[(targetRow * 4) + column] -=
                scale * matrix[(sourceRow * 4) + column];
        }
    }

    private static void ValidateDepthRange(float nearPlane, float farPlane) {
        if (!float.IsFinite(nearPlane) || nearPlane <= 0) {
            throw new ArgumentOutOfRangeException(nameof(nearPlane));
        }

        if (!float.IsFinite(farPlane) || farPlane <= nearPlane) {
            throw new ArgumentOutOfRangeException(nameof(farPlane));
        }
    }
}
