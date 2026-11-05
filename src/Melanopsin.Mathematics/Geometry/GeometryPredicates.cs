namespace Melanopsin.Mathematics;

/// <summary>
/// Internal predicates for geometry represented by single-precision
/// coordinates. Products are formed in double precision so a non-zero
/// orientation or determinant is not lost to intermediate single-precision
/// underflow merely because the inputs have a small magnitude.
/// </summary>
internal static class GeometryPredicates {
    public static double Cross(Vector2 left, Vector2 right) {
        return ((double)left.X * right.Y) - ((double)left.Y * right.X);
    }

    public static double Cross(
        Vector2 origin,
        Vector2 first,
        Vector2 second) {
        return Cross(first - origin, second - origin);
    }

    public static double Dot(Vector3 left, Vector3 right) {
        return ((double)left.X * right.X) +
            ((double)left.Y * right.Y) +
            ((double)left.Z * right.Z);
    }

    public static (double X, double Y, double Z) Cross(
        Vector3 left,
        Vector3 right) {
        return (
            ((double)left.Y * right.Z) - ((double)left.Z * right.Y),
            ((double)left.Z * right.X) - ((double)left.X * right.Z),
            ((double)left.X * right.Y) - ((double)left.Y * right.X));
    }

    public static double Dot(
        (double X, double Y, double Z) left,
        (double X, double Y, double Z) right) {
        return (left.X * right.X) +
            (left.Y * right.Y) +
            (left.Z * right.Z);
    }

    public static double LengthSquared(Vector2 value) {
        return ((double)value.X * value.X) + ((double)value.Y * value.Y);
    }

    public static double Length(Vector2 value) {
        return Math.Sqrt(LengthSquared(value));
    }

    public static double DistanceSquared(Vector2 left, Vector2 right) {
        var x = (double)left.X - right.X;
        var y = (double)left.Y - right.Y;
        return (x * x) + (y * y);
    }

    public static double Distance(Vector2 left, Vector2 right) {
        return Math.Sqrt(DistanceSquared(left, right));
    }

    public static double LengthSquared(Vector3 value) {
        return ((double)value.X * value.X) +
            ((double)value.Y * value.Y) +
            ((double)value.Z * value.Z);
    }

    public static double Length(Vector3 value) {
        return Math.Sqrt(LengthSquared(value));
    }

    public static double DistanceSquared(Vector3 left, Vector3 right) {
        var x = (double)left.X - right.X;
        var y = (double)left.Y - right.Y;
        var z = (double)left.Z - right.Z;
        return (x * x) + (y * y) + (z * z);
    }

    public static double Distance(Vector3 left, Vector3 right) {
        return Math.Sqrt(DistanceSquared(left, right));
    }
}
