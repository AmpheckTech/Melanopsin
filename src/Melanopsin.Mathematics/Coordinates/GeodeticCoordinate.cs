namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A geodetic latitude, longitude, and ellipsoidal height.
/// </summary>
/// <remarks>
/// Angles are expressed in degrees. Latitude is geodetic rather than
/// geocentric and is constrained to [-90, 90]. Longitude is constrained to
/// [-180, 180], with both endpoints representing the same meridian. Height is
/// measured along the ellipsoid normal in the units of the chosen ellipsoid,
/// normally meters for WGS84 and CGCS2000.
/// </remarks>
public readonly record struct GeodeticCoordinate {
    public GeodeticCoordinate(
        double latitudeDegrees,
        double longitudeDegrees,
        double height = 0) {
        if (!double.IsFinite(latitudeDegrees) ||
            latitudeDegrees < -90 ||
            latitudeDegrees > 90) {
            throw new ArgumentOutOfRangeException(nameof(latitudeDegrees));
        }

        if (!double.IsFinite(longitudeDegrees) ||
            longitudeDegrees < -180 ||
            longitudeDegrees > 180) {
            throw new ArgumentOutOfRangeException(nameof(longitudeDegrees));
        }

        if (!double.IsFinite(height)) {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        LatitudeDegrees = latitudeDegrees;
        LongitudeDegrees = longitudeDegrees;
        Height = height;
    }

    public double LatitudeDegrees { get; }

    public double LongitudeDegrees { get; }

    public double Height { get; }
}
