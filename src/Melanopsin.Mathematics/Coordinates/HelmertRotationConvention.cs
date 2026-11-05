namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// Specifies the sign convention used by a seven-parameter Helmert transform.
/// </summary>
/// <remarks>
/// The two conventions use the same parameter names but opposite rotation
/// signs. They are not interchangeable. The names follow the EPSG geocentric
/// transformation methods 1033 (position vector) and 1032 (coordinate frame).
/// </remarks>
public enum HelmertRotationConvention {
    /// <summary>
    /// Rotates the position vector within fixed geocentric axes.
    /// </summary>
    PositionVector = 0,

    /// <summary>
    /// Rotates the coordinate axes while the position vector remains fixed.
    /// </summary>
    CoordinateFrame = 1,
}
