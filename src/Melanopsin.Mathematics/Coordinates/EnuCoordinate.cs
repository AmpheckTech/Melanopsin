namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// A local East-North-Up coordinate in a right-handed tangent frame.
/// </summary>
public readonly record struct EnuCoordinate {
    public EnuCoordinate(double east, double north, double up) {
        if (!double.IsFinite(east) ||
            !double.IsFinite(north) ||
            !double.IsFinite(up)) {
            throw new ArgumentOutOfRangeException(nameof(east));
        }

        East = east;
        North = north;
        Up = up;
    }

    public double East { get; }

    public double North { get; }

    public double Up { get; }
}
