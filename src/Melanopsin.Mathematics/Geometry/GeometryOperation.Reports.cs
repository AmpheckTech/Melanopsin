namespace Melanopsin.Mathematics;

/// <summary>Identifies a geometric operation that can terminate without a result.</summary>
public enum GeometryOperation {
    /// <summary>Intersection of two axis-aligned bounding boxes.</summary>
    BoundsIntersection,

    /// <summary>Conservative transformation of an axis-aligned bounding box.</summary>
    BoundsTransform,

    /// <summary>Intersection of a two-dimensional ray and a bounding box.</summary>
    RayBoundsIntersection,

    /// <summary>Intersection of a two-dimensional ray and a segment.</summary>
    RaySegmentIntersection,

    /// <summary>Intersection of a three-dimensional ray and a bounding box.</summary>
    RayBoxIntersection,

    /// <summary>Intersection of a three-dimensional ray and a plane.</summary>
    RayPlaneIntersection,

    /// <summary>Intersection of a three-dimensional ray and a sphere.</summary>
    RaySphereIntersection,

    /// <summary>Intersection of a three-dimensional ray and a triangle.</summary>
    RayTriangleIntersection,

    /// <summary>Intersection of two line segments.</summary>
    SegmentIntersection,

    /// <summary>Intersection of a line segment and a bounding box.</summary>
    SegmentBoundsIntersection,

    /// <summary>Construction of a triangle normal.</summary>
    TriangleNormal,

    /// <summary>Evaluation of triangle barycentric coordinates.</summary>
    TriangleBarycentricCoordinates,

    /// <summary>Construction of a plane from three points.</summary>
    PlaneConstruction,

    /// <summary>Intersection of two planes.</summary>
    PlaneIntersection,

    /// <summary>Evaluation of a polygon centroid.</summary>
    PolygonCentroid,

    /// <summary>Triangulation of a simple polygon.</summary>
    PolygonTriangulation,

    /// <summary>Clipping of a polygon against a convex window.</summary>
    PolygonClipping,

    /// <summary>Construction of a frustum from a view-projection matrix.</summary>
    FrustumConstruction,

    /// <summary>Normalization of barycentric weights.</summary>
    BarycentricNormalization,
}

/// <summary>The termination status of a geometric operation.</summary>
public enum GeometryStatus {
    /// <summary>The operation produced a finite result.</summary>
    Succeeded,

    /// <summary>The geometric objects do not intersect.</summary>
    NoIntersection,

    /// <summary>A required geometric object is degenerate in the working precision.</summary>
    DegenerateGeometry,

    /// <summary>Required lines, planes, or directions are parallel or coincident.</summary>
    Parallel,

    /// <summary>An intersection exists only at a negative ray or segment parameter.</summary>
    BehindOrigin,

    /// <summary>An intersection exists outside the finite bounds of the primitive.</summary>
    OutsideBounds,

    /// <summary>An input contains a non-finite component.</summary>
    NonFiniteInput,

    /// <summary>A finite input produced a non-finite intermediate or result.</summary>
    NonFiniteOutput,

    /// <summary>A homogeneous point has zero or non-finite W and no Cartesian representative.</summary>
    ProjectiveSingularity,

    /// <summary>A required linear or differential map is singular.</summary>
    Singular,

    /// <summary>A tolerance or other argument violates the operation contract.</summary>
    InvalidInput,

    /// <summary>The operation failed numerically for a reason not covered by another status.</summary>
    NumericalFailure,
}

/// <summary>
/// A compact diagnostic report for geometric predicates and constructions.
/// </summary>
/// <remarks>
/// <para>
/// A failed operation distinguishes mathematical absence
/// (<see cref="GeometryStatus.NoIntersection"/>,
/// <see cref="GeometryStatus.Parallel"/>, and
/// <see cref="GeometryStatus.BehindOrigin"/>) from invalid input and numerical
/// failure. Callers that only need a predicate can continue to use the
/// <c>Try</c> overload without a report.
/// </para>
/// <para>
/// Singularity and degeneracy mean exactly zero in the working precision. The
/// library does not classify a finite non-zero determinant or area as singular
/// from an absolute threshold. Applications that need approximate predicates
/// must choose and apply their own scale-relative tolerance.
/// </para>
/// </remarks>
public readonly record struct GeometryOperationReport {
    internal GeometryOperationReport(
        GeometryOperation operation,
        GeometryStatus status) {
        Operation = operation;
        Status = status;
    }

    /// <summary>Gets the executed operation.</summary>
    public GeometryOperation Operation { get; }

    /// <summary>Gets the termination status.</summary>
    public GeometryStatus Status { get; }

    /// <summary>Gets whether the operation produced a usable result.</summary>
    public bool Succeeded => Status == GeometryStatus.Succeeded;
}
