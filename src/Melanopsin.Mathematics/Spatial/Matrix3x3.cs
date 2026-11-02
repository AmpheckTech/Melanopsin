using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A three-by-three matrix using row-major storage and column-vector
/// multiplication.
/// </summary>
public readonly record struct Matrix3x3 :
    IMultiplyOperators<Matrix3x3, Matrix3x3, Matrix3x3>,
    IMultiplyOperators<Matrix3x3, float, Matrix3x3>,
    IDivisionOperators<Matrix3x3, float, Matrix3x3>,
    IMultiplicativeIdentity<Matrix3x3, Matrix3x3>,
    IEqualityOperators<Matrix3x3, Matrix3x3, bool> {
    public Matrix3x3(
        float m11,
        float m12,
        float m13,
        float m21,
        float m22,
        float m23,
        float m31,
        float m32,
        float m33) {
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

    public static Matrix3x3 Identity => new(
        1, 0, 0,
        0, 1, 0,
        0, 0, 1);

    public static Matrix3x3 MultiplicativeIdentity => Identity;

    public float M11 { get; }

    public float M12 { get; }

    public float M13 { get; }

    public float M21 { get; }

    public float M22 { get; }

    public float M23 { get; }

    public float M31 { get; }

    public float M32 { get; }

    public float M33 { get; }

    public Vector3 Row1 => new(M11, M12, M13);

    public Vector3 Row2 => new(M21, M22, M23);

    public Vector3 Row3 => new(M31, M32, M33);

    public float Determinant =>
        (M11 * ((M22 * M33) - (M23 * M32))) -
        (M12 * ((M21 * M33) - (M23 * M31))) +
        (M13 * ((M21 * M32) - (M22 * M31)));

    public static Matrix3x3 CreateRotationX(float radians) {
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        return new Matrix3x3(
            1, 0, 0,
            0, cosine, -sine,
            0, sine, cosine);
    }

    public static Matrix3x3 CreateRotationY(float radians) {
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        return new Matrix3x3(
            cosine, 0, sine,
            0, 1, 0,
            -sine, 0, cosine);
    }

    public static Matrix3x3 CreateRotationZ(float radians) {
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        return new Matrix3x3(
            cosine, -sine, 0,
            sine, cosine, 0,
            0, 0, 1);
    }

    public static Matrix3x3 CreateFromQuaternion(Quaternion quaternion) {
        var quaternionValue = quaternion.Normalize();
        var x = quaternionValue.X;
        var y = quaternionValue.Y;
        var z = quaternionValue.Z;
        var w = quaternionValue.W;
        var xx = x * x;
        var yy = y * y;
        var zz = z * z;
        var xy = x * y;
        var xz = x * z;
        var yz = y * z;
        var wx = w * x;
        var wy = w * y;
        var wz = w * z;

        return new Matrix3x3(
            1 - (2 * (yy + zz)), 2 * (xy - wz), 2 * (xz + wy),
            2 * (xy + wz), 1 - (2 * (xx + zz)), 2 * (yz - wx),
            2 * (xz - wy), 2 * (yz + wx), 1 - (2 * (xx + yy)));
    }

    public Vector3 Transform(Vector3 value) {
        return new Vector3(
            (M11 * value.X) + (M12 * value.Y) + (M13 * value.Z),
            (M21 * value.X) + (M22 * value.Y) + (M23 * value.Z),
            (M31 * value.X) + (M32 * value.Y) + (M33 * value.Z));
    }

    public Matrix3x3 Transpose() {
        return new Matrix3x3(
            M11, M21, M31,
            M12, M22, M32,
            M13, M23, M33);
    }

    public Matrix3x3 Invert() {
        if (!TryInvert(out var inverse)) {
            throw new InvalidOperationException("The matrix is singular.");
        }

        return inverse;
    }

    public bool TryInvert(out Matrix3x3 inverse) {
        var determinant = Determinant;
        if (!float.IsFinite(determinant) || MathF.Abs(determinant) <= ScalarMath.Epsilon) {
            inverse = default;
            return false;
        }

        var inverseDeterminant = 1 / determinant;
        inverse = new Matrix3x3(
            ((M22 * M33) - (M23 * M32)) * inverseDeterminant,
            ((M13 * M32) - (M12 * M33)) * inverseDeterminant,
            ((M12 * M23) - (M13 * M22)) * inverseDeterminant,
            ((M23 * M31) - (M21 * M33)) * inverseDeterminant,
            ((M11 * M33) - (M13 * M31)) * inverseDeterminant,
            ((M13 * M21) - (M11 * M23)) * inverseDeterminant,
            ((M21 * M32) - (M22 * M31)) * inverseDeterminant,
            ((M12 * M31) - (M11 * M32)) * inverseDeterminant,
            ((M11 * M22) - (M12 * M21)) * inverseDeterminant);
        return true;
    }

    public static Matrix3x3 operator *(
        Matrix3x3 left,
        Matrix3x3 right) {
        return new Matrix3x3(
            (left.M11 * right.M11) + (left.M12 * right.M21) + (left.M13 * right.M31),
            (left.M11 * right.M12) + (left.M12 * right.M22) + (left.M13 * right.M32),
            (left.M11 * right.M13) + (left.M12 * right.M23) + (left.M13 * right.M33),
            (left.M21 * right.M11) + (left.M22 * right.M21) + (left.M23 * right.M31),
            (left.M21 * right.M12) + (left.M22 * right.M22) + (left.M23 * right.M32),
            (left.M21 * right.M13) + (left.M22 * right.M23) + (left.M23 * right.M33),
            (left.M31 * right.M11) + (left.M32 * right.M21) + (left.M33 * right.M31),
            (left.M31 * right.M12) + (left.M32 * right.M22) + (left.M33 * right.M32),
            (left.M31 * right.M13) + (left.M32 * right.M23) + (left.M33 * right.M33));
    }

    public static Vector3 operator *(Matrix3x3 matrix, Vector3 value) {
        return matrix.Transform(value);
    }

    public static Matrix3x3 operator *(Matrix3x3 matrix, float scale) {
        return new Matrix3x3(
            matrix.M11 * scale, matrix.M12 * scale, matrix.M13 * scale,
            matrix.M21 * scale, matrix.M22 * scale, matrix.M23 * scale,
            matrix.M31 * scale, matrix.M32 * scale, matrix.M33 * scale);
    }

    public static Matrix3x3 operator *(float scale, Matrix3x3 matrix) {
        return matrix * scale;
    }

    public static Matrix3x3 operator /(Matrix3x3 matrix, float divisor) {
        return new Matrix3x3(
            matrix.M11 / divisor, matrix.M12 / divisor, matrix.M13 / divisor,
            matrix.M21 / divisor, matrix.M22 / divisor, matrix.M23 / divisor,
            matrix.M31 / divisor, matrix.M32 / divisor, matrix.M33 / divisor);
    }
}
