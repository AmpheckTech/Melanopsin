namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// Conversions between Cartesian, cylindrical, and spherical coordinates.
/// </summary>
/// <remarks>
/// Conversions use a common Z-up axis. Right-handed frames increase azimuth
/// from positive X toward positive Y. Left-handed frames increase azimuth from
/// positive X toward negative Y. The inverse conversion intentionally returns
/// the signed convention of the selected frame, so a round trip preserves the
/// position but may normalize an azimuth outside [-pi, pi].
/// </remarks>
public static class CoordinateTransforms {
    public static CartesianCoordinate3 ToCartesian(
        CylindricalCoordinate coordinate,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        var (x, y) = Transverse(
            coordinate.Radius,
            coordinate.AzimuthRadians,
            handedness);
        return new CartesianCoordinate3(x, y, coordinate.Z);
    }

    public static CylindricalCoordinate ToCylindrical(
        CartesianCoordinate3 coordinate,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        var radius = ElementaryFunctions.Hypot(coordinate.X, coordinate.Y);
        var azimuth = radius == 0 ? 0 : Azimuth(coordinate.X, coordinate.Y, handedness);
        return new CylindricalCoordinate(radius, azimuth, coordinate.Z);
    }

    public static CartesianCoordinate3 ToCartesian(
        SphericalCoordinate coordinate,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        var transverse = coordinate.RadialDistance *
            Math.Sin(coordinate.PolarAngleRadians);
        var (x, y) = Transverse(
            transverse,
            coordinate.AzimuthRadians,
            handedness);
        var z = coordinate.RadialDistance *
            Math.Cos(coordinate.PolarAngleRadians);
        return new CartesianCoordinate3(x, y, z);
    }

    public static SphericalCoordinate ToSpherical(
        CartesianCoordinate3 coordinate,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        var radius = coordinate.Radius;
        if (radius == 0) {
            return new SphericalCoordinate(0, 0, 0);
        }

        var transverse = ElementaryFunctions.Hypot(coordinate.X, coordinate.Y);
        var polar = Math.Atan2(transverse, coordinate.Z);
        var azimuth = transverse == 0
            ? 0
            : Azimuth(coordinate.X, coordinate.Y, handedness);
        return new SphericalCoordinate(radius, polar, azimuth);
    }

    public static CylindricalCoordinate ToCylindrical(
        SphericalCoordinate coordinate,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        return ToCylindrical(ToCartesian(coordinate, handedness), handedness);
    }

    public static SphericalCoordinate ToSpherical(
        CylindricalCoordinate coordinate,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        return ToSpherical(ToCartesian(coordinate, handedness), handedness);
    }

    private static (double X, double Y) Transverse(
        double radius,
        double azimuth,
        CoordinateHandedness handedness) {
        var x = radius * Math.Cos(azimuth);
        var y = radius * Math.Sin(azimuth);
        return handedness == CoordinateHandedness.RightHanded
            ? (x, y)
            : (x, -y);
    }

    private static double Azimuth(
        double x,
        double y,
        CoordinateHandedness handedness) {
        return handedness == CoordinateHandedness.RightHanded
            ? Math.Atan2(y, x)
            : Math.Atan2(-y, x);
    }
}
