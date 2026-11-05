namespace Melanopsin.Mathematics;

/// <summary>
/// A plane represented by the normalized equation
/// <c>dot(Normal, point) + Distance = 0</c>.
/// </summary>
public readonly partial record struct Plane3 {
    public Plane3(Vector3 normal, float distance) {
        if (!normal.IsFinite || !float.IsFinite(distance)) {
            throw new ArgumentException(
                "Plane coefficients must be finite.",
                nameof(normal));
        }

        var x = (double)normal.X;
        var y = (double)normal.Y;
        var z = (double)normal.Z;
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        if (length == 0) {
            throw new ArgumentException(
                "A plane normal must be non-zero.",
                nameof(normal));
        }

        var normalizedX = x / length;
        var normalizedY = y / length;
        var normalizedZ = z / length;
        var scaledDistance = distance / length;
        if (!MatrixNumerics.IsRepresentableAsFloat(normalizedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedZ) ||
            !MatrixNumerics.IsRepresentableAsFloat(scaledDistance)) {
            throw new ArgumentException(
                "The normalized plane coefficients are not representable.",
                nameof(normal));
        }

        Normal = new Vector3(
            (float)normalizedX,
            (float)normalizedY,
            (float)normalizedZ);
        Distance = (float)scaledDistance;
    }

    public Vector3 Normal { get; }

    public float Distance { get; }

    public static Plane3 FromPointNormal(Vector3 point, Vector3 normal) {
        if (!TryFromPointNormal(point, normal, out var plane, out var report)) {
            if (report.Status == GeometryStatus.NonFiniteInput) {
                throw new ArgumentException(
                    "The point and normal must be finite.",
                    nameof(normal));
            }

            throw new ArithmeticException(
                $"The plane could not be constructed: {report.Status}.");
        }

        return plane;
    }

    /// <summary>
    /// Attempts to construct a plane from a point and normal and reports
    /// non-finite input or a zero normal.
    /// </summary>
    public static bool TryFromPointNormal(
        Vector3 point,
        Vector3 normal,
        out Plane3 plane,
        out GeometryOperationReport report) {
        if (!point.IsFinite || !normal.IsFinite) {
            plane = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneConstruction,
                GeometryStatus.NonFiniteInput);
            return false;
        }

        double x = normal.X;
        double y = normal.Y;
        double z = normal.Z;
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        if (length == 0) {
            plane = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneConstruction,
                GeometryStatus.DegenerateGeometry);
            return false;
        }

        var normalizedX = x / length;
        var normalizedY = y / length;
        var normalizedZ = z / length;
        var distance = -(
            (normalizedX * point.X) +
            (normalizedY * point.Y) +
            (normalizedZ * point.Z));
        if (!MatrixNumerics.IsRepresentableAsFloat(normalizedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedZ) ||
            !MatrixNumerics.IsRepresentableAsFloat(distance)) {
            plane = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneConstruction,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        plane = new Plane3(
            new Vector3(
                (float)normalizedX,
                (float)normalizedY,
                (float)normalizedZ),
            (float)distance);
        report = new GeometryOperationReport(
            GeometryOperation.PlaneConstruction,
            GeometryStatus.Succeeded);
        return true;
    }

    public static bool TryFromPoints(
        Vector3 first,
        Vector3 second,
        Vector3 third,
        out Plane3 plane) {
        return TryFromPoints(first, second, third, out plane, out _);
    }

    /// <summary>
    /// Attempts to construct a plane from three points and reports non-finite
    /// input or collinear geometry.
    /// </summary>
    public static bool TryFromPoints(
        Vector3 first,
        Vector3 second,
        Vector3 third,
        out Plane3 plane,
        out GeometryOperationReport report) {
        if (!first.IsFinite || !second.IsFinite || !third.IsFinite) {
            plane = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneConstruction,
                GeometryStatus.NonFiniteInput);
            return false;
        }

        double firstX = (double)second.X - first.X;
        double firstY = (double)second.Y - first.Y;
        double firstZ = (double)second.Z - first.Z;
        double secondX = (double)third.X - first.X;
        double secondY = (double)third.Y - first.Y;
        double secondZ = (double)third.Z - first.Z;
        var normalX = (firstY * secondZ) - (firstZ * secondY);
        var normalY = (firstZ * secondX) - (firstX * secondZ);
        var normalZ = (firstX * secondY) - (firstY * secondX);
        var normalLength = Math.Sqrt(
            (normalX * normalX) +
            (normalY * normalY) +
            (normalZ * normalZ));
        if (!double.IsFinite(normalLength)) {
            plane = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneConstruction,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (normalLength == 0) {
            plane = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneConstruction,
                GeometryStatus.DegenerateGeometry);
            return false;
        }

        var normalizedX = normalX / normalLength;
        var normalizedY = normalY / normalLength;
        var normalizedZ = normalZ / normalLength;
        var distance = -(
            (normalizedX * first.X) +
            (normalizedY * first.Y) +
            (normalizedZ * first.Z));
        if (!MatrixNumerics.IsRepresentableAsFloat(normalizedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedZ) ||
            !MatrixNumerics.IsRepresentableAsFloat(distance)) {
            plane = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneConstruction,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        plane = new Plane3(
            new Vector3(
                (float)normalizedX,
                (float)normalizedY,
                (float)normalizedZ),
            (float)distance);
        report = new GeometryOperationReport(
            GeometryOperation.PlaneConstruction,
            GeometryStatus.Succeeded);
        return true;
    }

    public float SignedDistance(Vector3 point) {
        return (float)SignedDistanceCore(point);
    }

    public float DistanceTo(Vector3 point) {
        return (float)Math.Abs(SignedDistanceCore(point));
    }

    public Vector3 Project(Vector3 point) {
        var distance = SignedDistanceCore(point);
        return new Vector3(
            (float)((double)point.X - (Normal.X * distance)),
            (float)((double)point.Y - (Normal.Y * distance)),
            (float)((double)point.Z - (Normal.Z * distance)));
    }

    public Vector3 Reflect(Vector3 point) {
        var distance = 2 * SignedDistanceCore(point);
        return new Vector3(
            (float)((double)point.X - (Normal.X * distance)),
            (float)((double)point.Y - (Normal.Y * distance)),
            (float)((double)point.Z - (Normal.Z * distance)));
    }

    public Vector3 ReflectDirection(Vector3 direction) {
        var scale = 2 * (
            ((double)Normal.X * direction.X) +
            ((double)Normal.Y * direction.Y) +
            ((double)Normal.Z * direction.Z));
        return new Vector3(
            (float)((double)direction.X - (Normal.X * scale)),
            (float)((double)direction.Y - (Normal.Y * scale)),
            (float)((double)direction.Z - (Normal.Z * scale)));
    }

    public bool TryIntersect(Plane3 other, out Ray3 line) {
        return TryIntersect(other, out line, out _);
    }

    /// <summary>
    /// Attempts to intersect two planes and reports parallel planes or a
    /// numerically indeterminate intersection.
    /// </summary>
    public bool TryIntersect(
        Plane3 other,
        out Ray3 line,
        out GeometryOperationReport report) {
        var directionX =
            ((double)Normal.Y * other.Normal.Z) -
            ((double)Normal.Z * other.Normal.Y);
        var directionY =
            ((double)Normal.Z * other.Normal.X) -
            ((double)Normal.X * other.Normal.Z);
        var directionZ =
            ((double)Normal.X * other.Normal.Y) -
            ((double)Normal.Y * other.Normal.X);
        var directionLengthSquared = (directionX * directionX) +
            (directionY * directionY) +
            (directionZ * directionZ);
        if (directionLengthSquared == 0) {
            line = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneIntersection,
                GeometryStatus.Parallel);
            return false;
        }

        var first = ((double)Normal.X * Normal.X) +
            ((double)Normal.Y * Normal.Y) +
            ((double)Normal.Z * Normal.Z);
        var second = ((double)other.Normal.X * other.Normal.X) +
            ((double)other.Normal.Y * other.Normal.Y) +
            ((double)other.Normal.Z * other.Normal.Z);
        var dot = ((double)Normal.X * other.Normal.X) +
            ((double)Normal.Y * other.Normal.Y) +
            ((double)Normal.Z * other.Normal.Z);
        var denominator = (first * second) - (dot * dot);
        if (denominator == 0) {
            line = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneIntersection,
                GeometryStatus.Parallel);
            return false;
        }

        var firstScale = ((other.Distance * dot) - (Distance * second)) / denominator;
        var secondScale = ((Distance * dot) - (other.Distance * first)) / denominator;
        var pointX = (firstScale * Normal.X) + (secondScale * other.Normal.X);
        var pointY = (firstScale * Normal.Y) + (secondScale * other.Normal.Y);
        var pointZ = (firstScale * Normal.Z) + (secondScale * other.Normal.Z);
        var directionLength = Math.Sqrt(directionLengthSquared);
        var normalizedX = directionX / directionLength;
        var normalizedY = directionY / directionLength;
        var normalizedZ = directionZ / directionLength;
        if (!MatrixNumerics.IsRepresentableAsFloat(pointX) ||
            !MatrixNumerics.IsRepresentableAsFloat(pointY) ||
            !MatrixNumerics.IsRepresentableAsFloat(pointZ) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedX) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedY) ||
            !MatrixNumerics.IsRepresentableAsFloat(normalizedZ)) {
            line = default;
            report = new GeometryOperationReport(
                GeometryOperation.PlaneIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        line = new Ray3(
            new Vector3((float)pointX, (float)pointY, (float)pointZ),
            new Vector3(
                (float)normalizedX,
                (float)normalizedY,
                (float)normalizedZ));
        report = new GeometryOperationReport(
            GeometryOperation.PlaneIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    internal double SignedDistanceCore(Vector3 point) {
        return
            ((double)Normal.X * point.X) +
            ((double)Normal.Y * point.Y) +
            ((double)Normal.Z * point.Z) +
            Distance;
    }
}
