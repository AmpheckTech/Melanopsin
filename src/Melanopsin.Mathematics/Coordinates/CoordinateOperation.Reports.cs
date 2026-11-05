namespace Melanopsin.Mathematics.Coordinates;

/// <summary>Identifies a coordinate-frame or geodetic transformation operation.</summary>
public enum CoordinateOperation {
    /// <summary>Construction from independent X and Y axes.</summary>
    FrameFromAxes,

    /// <summary>Construction from an exact Z axis and X-axis hint.</summary>
    FrameFromZAxis,

    /// <summary>Local point to reference-frame point.</summary>
    PointToReference,

    /// <summary>Reference-frame point to local point.</summary>
    PointFromReference,

    /// <summary>Local direction to reference-frame direction.</summary>
    DirectionToReference,

    /// <summary>Reference-frame direction to local direction.</summary>
    DirectionFromReference,

    /// <summary>Point transformation between two coordinate frames.</summary>
    FramePointTransform,

    /// <summary>Direction transformation between two coordinate frames.</summary>
    FrameDirectionTransform,

    /// <summary>Frame-pose transformation between two coordinate frames.</summary>
    FrameTransform,

    /// <summary>Shortest registered geodetic operation-path resolution.</summary>
    GeodeticPathResolution,

    /// <summary>Geodetic coordinate transformation through a registered path.</summary>
    GeodeticTransform,

    /// <summary>Forward transformation by a single geodetic coordinate operation.</summary>
    GeodeticForward,

    /// <summary>Algebraic inverse of a single geodetic coordinate operation.</summary>
    GeodeticInverse,

    /// <summary>Forward seven-parameter Helmert transformation.</summary>
    HelmertForward,

    /// <summary>Algebraic inverse seven-parameter Helmert transformation.</summary>
    HelmertInverse,

    /// <summary>Forward composite datum transformation.</summary>
    DatumForward,

    /// <summary>Algebraic inverse composite datum transformation.</summary>
    DatumInverse,

    /// <summary>Inverse geodesic distance solved by Vincenty's iteration.</summary>
    VincentyInverse,

    /// <summary>Geodetic latitude, longitude, and height to Earth-fixed Cartesian.</summary>
    GeodeticToEcef,

    /// <summary>Earth-fixed Cartesian to geodetic latitude, longitude, and height.</summary>
    EcefToGeodetic,

    /// <summary>Normalization of a three-dimensional direction vector.</summary>
    VectorNormalization,

    /// <summary>Forward ellipsoidal Mercator projection.</summary>
    MercatorForward,

    /// <summary>Inverse ellipsoidal Mercator projection.</summary>
    MercatorInverse,

    /// <summary>Forward ellipsoidal transverse Mercator projection.</summary>
    TransverseMercatorForward,

    /// <summary>Inverse ellipsoidal transverse Mercator projection.</summary>
    TransverseMercatorInverse,
}

/// <summary>The termination status of a coordinate operation.</summary>
public enum CoordinateOperationStatus {
    /// <summary>The operation completed and produced a finite result or path.</summary>
    Succeeded,

    /// <summary>The source coordinate reference system is not registered.</summary>
    UnknownSource,

    /// <summary>The target coordinate reference system is not registered.</summary>
    UnknownTarget,

    /// <summary>No registered operation path connects the endpoints.</summary>
    NoPath,

    /// <summary>The operation does not define an algebraic inverse.</summary>
    NotInvertible,

    /// <summary>A direction, axis, endpoint, or coefficient is invalid.</summary>
    InvalidInput,

    /// <summary>A direction or basis is zero or geometrically degenerate.</summary>
    DegenerateGeometry,

    /// <summary>An input coordinate, direction, or parameter is not finite.</summary>
    NonFiniteInput,

    /// <summary>A coordinate lies outside the defined domain of the projection.</summary>
    OutOfDomain,

    /// <summary>A finite input produced a non-finite result.</summary>
    NonFiniteOutput,

    /// <summary>A required linear map is exactly singular in the working precision.</summary>
    Singular,

    /// <summary>The operation failed numerically for a reason other than singularity.</summary>
    NumericalFailure,

    /// <summary>An iterative numerical method exhausted its budget without converging.</summary>
    NonConverged,
}

/// <summary>
/// A compact diagnostic report shared by coordinate-frame, Helmert, datum, and
/// registry-path operations.
/// </summary>
/// <remarks>
/// A successful Helmert result does not imply a particular accuracy or
/// conditioning. Singularity is reported only when the required determinant or
/// inverse coefficient is exactly zero or non-finite in the working precision;
/// no absolute near-singularity threshold is applied.
/// </remarks>
public readonly record struct CoordinateOperationReport {
    internal CoordinateOperationReport(
        CoordinateOperation operation,
        CoordinateOperationStatus status,
        int directStepCount = 0,
        int inverseStepCount = 0) {
        Operation = operation;
        Status = status;
        DirectStepCount = directStepCount;
        InverseStepCount = inverseStepCount;
    }

    /// <summary>Gets the executed operation.</summary>
    public CoordinateOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public CoordinateOperationStatus Status { get; }

    /// <summary>Gets whether the operation produced a usable finite result.</summary>
    public bool Succeeded => Status == CoordinateOperationStatus.Succeeded;

    /// <summary>Gets the number of forward operation steps in the resolved path.</summary>
    public int DirectStepCount { get; }

    /// <summary>Gets the number of inverse operation steps in the resolved path.</summary>
    public int InverseStepCount { get; }

    /// <summary>Gets the total number of resolved operation steps.</summary>
    public int StepCount => DirectStepCount + InverseStepCount;
}
