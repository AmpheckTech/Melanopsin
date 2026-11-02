using System.Numerics;

namespace Melanopsin.Mathematics;

/// <summary>
/// A unit quaternion used for three-dimensional rotation.
/// </summary>
public readonly record struct Quaternion :
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

    public float LengthSquared =>
        (X * X) + (Y * Y) + (Z * Z) + (W * W);

    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z) &&
        float.IsFinite(W);

    public static float Dot(Quaternion left, Quaternion right) {
        return (left.X * right.X) +
            (left.Y * right.Y) +
            (left.Z * right.Z) +
            (left.W * right.W);
    }

    public static Quaternion CreateFromAxisAngle(Vector3 axis, float radians) {
        if (!axis.TryNormalize(out var normal)) {
            throw new ArgumentException(
                "The rotation axis must be finite and non-zero.",
                nameof(axis));
        }

        if (!float.IsFinite(radians)) {
            throw new ArgumentOutOfRangeException(nameof(radians));
        }

        var halfAngle = radians / 2;
        var sine = MathF.Sin(halfAngle);
        return new Quaternion(
            normal.X * sine,
            normal.Y * sine,
            normal.Z * sine,
            MathF.Cos(halfAngle));
    }

    public static Quaternion CreateFromYawPitchRoll(
        float yaw,
        float pitch,
        float roll) {
        return CreateFromAxisAngle(Vector3.UnitY, yaw) *
            CreateFromAxisAngle(Vector3.UnitX, pitch) *
            CreateFromAxisAngle(Vector3.UnitZ, roll);
    }

    public Quaternion Normalize() {
        var lengthSquared = LengthSquared;
        if (!float.IsFinite(lengthSquared) || lengthSquared <= ScalarMath.EpsilonSquared) {
            throw new InvalidOperationException(
                "A zero or non-finite quaternion cannot be normalized.");
        }

        var inverseLength = 1 / MathF.Sqrt(lengthSquared);
        return new Quaternion(
            X * inverseLength,
            Y * inverseLength,
            Z * inverseLength,
            W * inverseLength);
    }

    public bool TryNormalize(out Quaternion result) {
        var lengthSquared = LengthSquared;
        if (!float.IsFinite(lengthSquared) || lengthSquared <= ScalarMath.EpsilonSquared) {
            result = Identity;
            return false;
        }

        var inverseLength = 1 / MathF.Sqrt(lengthSquared);
        result = new Quaternion(
            X * inverseLength,
            Y * inverseLength,
            Z * inverseLength,
            W * inverseLength);
        return true;
    }

    public Quaternion Conjugate() {
        return new Quaternion(-X, -Y, -Z, W);
    }

    public Quaternion Inverse() {
        var lengthSquared = LengthSquared;
        if (!float.IsFinite(lengthSquared) || lengthSquared <= ScalarMath.EpsilonSquared) {
            throw new InvalidOperationException(
                "A zero or non-finite quaternion has no inverse.");
        }

        var conjugate = Conjugate();
        return new Quaternion(
            conjugate.X / lengthSquared,
            conjugate.Y / lengthSquared,
            conjugate.Z / lengthSquared,
            conjugate.W / lengthSquared);
    }

    public Vector3 Rotate(Vector3 value) {
        var normalized = Normalize();
        var imaginary = new Vector3(normalized.X, normalized.Y, normalized.Z);
        var twiceCross = Vector3.Cross(imaginary, value) * 2;
        return value + (twiceCross * normalized.W) +
            Vector3.Cross(imaginary, twiceCross);
    }

    public static Quaternion operator *(
        Quaternion left,
        Quaternion right) {
        return new Quaternion(
            (left.W * right.X) + (left.X * right.W) +
                (left.Y * right.Z) - (left.Z * right.Y),
            (left.W * right.Y) - (left.X * right.Z) +
                (left.Y * right.W) + (left.Z * right.X),
            (left.W * right.Z) + (left.X * right.Y) -
                (left.Y * right.X) + (left.Z * right.W),
            (left.W * right.W) - (left.X * right.X) -
                (left.Y * right.Y) - (left.Z * right.Z));
    }

    public static Quaternion operator +(Quaternion left, Quaternion right) {
        return new Quaternion(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z,
            left.W + right.W);
    }

    public static Quaternion operator -(Quaternion left, Quaternion right) {
        return new Quaternion(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z,
            left.W - right.W);
    }

    public static Quaternion operator -(Quaternion value) {
        return new Quaternion(-value.X, -value.Y, -value.Z, -value.W);
    }

    public static Quaternion operator *(Quaternion value, float scale) {
        return new Quaternion(
            value.X * scale,
            value.Y * scale,
            value.Z * scale,
            value.W * scale);
    }

    public static Quaternion operator *(float scale, Quaternion value) {
        return value * scale;
    }

    public static Quaternion operator /(Quaternion value, float divisor) {
        return new Quaternion(
            value.X / divisor,
            value.Y / divisor,
            value.Z / divisor,
            value.W / divisor);
    }
}
