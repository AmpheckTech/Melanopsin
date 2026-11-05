// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;

namespace Melanopsin.Mathematics;

/// <summary>
/// A unit quaternion used for three-dimensional rotation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct Quaternion :
    IAdditionOperators<Quaternion, Quaternion, Quaternion>,
    ISubtractionOperators<Quaternion, Quaternion, Quaternion>,
    IUnaryNegationOperators<Quaternion, Quaternion>,
    IMultiplyOperators<Quaternion, Quaternion, Quaternion>,
    IMultiplyOperators<Quaternion, float, Quaternion>,
    IDivisionOperators<Quaternion, float, Quaternion>,
    IAdditiveIdentity<Quaternion, Quaternion>,
    IMultiplicativeIdentity<Quaternion, Quaternion>,
    IEqualityOperators<Quaternion, Quaternion, bool> {
    public Quaternion(float x, float y, float z, float w) {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Quaternion Identity => new(0, 0, 0, 1);

    public static Quaternion AdditiveIdentity => new(0, 0, 0, 0);

    public static Quaternion MultiplicativeIdentity => Identity;

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public float W { get; }

    /// <summary>
    /// Gets the squared norm, evaluated with double intermediates before
    /// rounding to <see cref="float"/>.
    /// </summary>
    public float LengthSquared => (float)(
        ((double)X * X) +
        ((double)Y * Y) +
        ((double)Z * Z) +
        ((double)W * W));

    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z) &&
        float.IsFinite(W);

    public static float Dot(Quaternion left, Quaternion right) {
        return (float)(
            ((double)left.X * right.X) +
            ((double)left.Y * right.Y) +
            ((double)left.Z * right.Z) +
            ((double)left.W * right.W));
    }

    public static Quaternion CreateFromAxisAngle(Vector3 axis, float radians) {
        if (!float.IsFinite(radians)) {
            throw new ArgumentOutOfRangeException(nameof(radians));
        }

        if (!TryCreateFromAxisAngle(
                axis,
                radians,
                out var result,
                out _)) {
            throw new ArgumentException(
                "The rotation axis must be finite and non-zero.",
                nameof(axis));
        }

        return result;
    }

    public static Quaternion CreateFromYawPitchRoll(
        float yaw,
        float pitch,
        float roll) {
        return CreateFromAxisAngle(Vector3.UnitY, yaw) *
            CreateFromAxisAngle(Vector3.UnitX, pitch) *
            CreateFromAxisAngle(Vector3.UnitZ, roll);
    }

    /// <summary>
    /// Creates a quaternion from an orthonormal right-handed rotation matrix.
    /// </summary>
    /// <remarks>
    /// The matrix uses row-major storage, column-vector multiplication, and a
    /// right-handed coordinate convention. This method does not orthonormalize
    /// arbitrary matrices; callers must pass a rotation matrix, such as one
    /// returned by <see cref="Matrix3x3.CreateFromQuaternion"/> or an affine
    /// decomposition. A non-finite or non-invertible matrix is rejected.
    /// </remarks>
    public static Quaternion CreateFromRotationMatrix(Matrix3x3 matrix) {
        if (!TryCreateFromRotationMatrix(
                matrix,
                out var result,
                out var report)) {
            throw new ArgumentException(
                report.Status == SpatialOperationStatus.Singular
                    ? "The matrix does not define a finite rotation."
                    : "The matrix does not define a representable rotation.",
                nameof(matrix));
        }

        return result;
    }

    public Quaternion Normalize() {
        if (!TryNormalize(out var result)) {
            throw new ArithmeticException(
                "A zero or non-finite quaternion cannot be normalized.");
        }

        return result;
    }

    public bool TryNormalize(out Quaternion result) {
        return TryNormalize(out result, out _);
    }

    public Quaternion Conjugate() {
        return new Quaternion(-X, -Y, -Z, W);
    }

    public Quaternion Inverse() {
        if (!TryInvert(out var result)) {
            throw new ArithmeticException(
                "A zero or non-finite quaternion has no inverse.");
        }

        return result;
    }

    public Vector3 Rotate(Vector3 value) {
        if (!TryRotate(value, out var result, out var report)) {
            throw new ArithmeticException(
                report.Status == SpatialOperationStatus.NonFiniteInput
                    ? "A finite quaternion and vector are required for rotation."
                    : "A zero or non-finite quaternion cannot define a rotation.");
        }

        return result;
    }

    public static Quaternion Add(Quaternion left, Quaternion right) {
        return new Quaternion(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z,
            left.W + right.W);
    }

    public static Quaternion Subtract(Quaternion left, Quaternion right) {
        return new Quaternion(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z,
            left.W - right.W);
    }

    public static Quaternion Negate(Quaternion value) {
        return new Quaternion(-value.X, -value.Y, -value.Z, -value.W);
    }

    public static Quaternion operator *(
        Quaternion left,
        Quaternion right) {
        return Multiply(left, right);
    }

    /// <summary>
    /// Multiplies two quaternions using double intermediates before rounding to
    /// <see cref="float"/>.
    /// </summary>
    public static Quaternion Multiply(Quaternion left, Quaternion right) {
        return new Quaternion(
            (float)(
                ((double)left.W * right.X) + ((double)left.X * right.W) +
                ((double)left.Y * right.Z) - ((double)left.Z * right.Y)),
            (float)(
                ((double)left.W * right.Y) - ((double)left.X * right.Z) +
                ((double)left.Y * right.W) + ((double)left.Z * right.X)),
            (float)(
                ((double)left.W * right.Z) + ((double)left.X * right.Y) -
                ((double)left.Y * right.X) + ((double)left.Z * right.W)),
            (float)(
                ((double)left.W * right.W) - ((double)left.X * right.X) -
                ((double)left.Y * right.Y) - ((double)left.Z * right.Z)));
    }

    public static Quaternion Multiply(Quaternion value, float scale) {
        return new Quaternion(
            value.X * scale,
            value.Y * scale,
            value.Z * scale,
            value.W * scale);
    }

    public static Quaternion Multiply(float scale, Quaternion value) {
        return Multiply(value, scale);
    }

    public static Quaternion Divide(Quaternion value, float divisor) {
        return new Quaternion(
            value.X / divisor,
            value.Y / divisor,
            value.Z / divisor,
            value.W / divisor);
    }

    public static Quaternion operator +(Quaternion left, Quaternion right) {
        return Add(left, right);
    }

    public static Quaternion operator -(Quaternion left, Quaternion right) {
        return Subtract(left, right);
    }

    public static Quaternion operator -(Quaternion value) {
        return Negate(value);
    }

    public static Quaternion operator *(Quaternion value, float scale) {
        return Multiply(value, scale);
    }

    public static Quaternion operator *(float scale, Quaternion value) {
        return Multiply(scale, value);
    }

    public static Quaternion operator /(Quaternion value, float divisor) {
        return Divide(value, divisor);
    }
}
