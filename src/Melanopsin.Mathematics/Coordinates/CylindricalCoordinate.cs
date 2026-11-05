namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A cylindrical coordinate with radius, signed azimuth, and axial coordinate.
/// </summary>
/// <remarks>
/// The axial axis is positive Z. Azimuth is measured from the positive X axis
/// in radians. Handedness changes whether increasing azimuth is toward positive
/// or negative Y; see <see cref="CoordinateTransforms"/>.
/// </remarks>
public readonly record struct CylindricalCoordinate {
    public CylindricalCoordinate(double radius, double azimuthRadians, double z) {
        if (!double.IsFinite(radius) || radius < 0) {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (!double.IsFinite(azimuthRadians)) {
            throw new ArgumentOutOfRangeException(nameof(azimuthRadians));
        }

        if (!double.IsFinite(z)) {
            throw new ArgumentOutOfRangeException(nameof(z));
        }

        Radius = radius;
        AzimuthRadians = azimuthRadians;
        Z = z;
    }

    public double Radius { get; }

    public double AzimuthRadians { get; }

    public double Z { get; }
}
