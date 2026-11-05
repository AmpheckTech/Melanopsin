namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// One directed step in a resolved geodetic coordinate operation path.
/// </summary>
public readonly record struct GeodeticCoordinateOperationStep(
    GeodeticCoordinateOperation Operation,
    bool IsInverse);
