using Melanopsin.Mathematics.Coordinates;

namespace Melanopsin.Mathematics.Optics;

/// <summary>
/// Coordinate-frame conventions and transforms for sequential optical systems.
/// </summary>
/// <remarks>
/// <see cref="Global"/> is the canonical right-handed global frame. It does not
/// privilege an optical axis: sequential systems normally choose local positive
/// Z along nominal propagation and local X/Y as the transverse axes. Element and
/// surface frames are static poses whose origin and axes are expressed in a
/// caller-selected reference frame, normally global. Ray frames are local frames
/// whose positive Z axis follows the propagation direction.
/// <para>
/// Element and surface frames use identical orthonormal frame mathematics; the
/// separate factories exist to make the modeling intent explicit. When a
/// surface pose is specified relative to an element, use
/// <see cref="TransformFrame"/> with the element frame as the source and
/// <see cref="Global"/> as the destination. The library does not impose a
/// scene graph or implicit parent chain.
/// </para>
/// </remarks>
public static class OpticalCoordinateSystems {
    /// <summary>
    /// Gets the canonical right-handed global optical frame.
    /// </summary>
    public static CoordinateFrame3 Global => CoordinateFrame3.Identity;

    /// <summary>
    /// Constructs an optical element frame with local Z as its axial direction.
    /// </summary>
    public static CoordinateFrame3 CreateElementFrame(
        CartesianCoordinate3 origin,
        Vector3<double> localZAxis,
        Vector3<double> localXAxisHint,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        return CoordinateFrame3.CreateFromZAxis(
            origin,
            localZAxis,
            localXAxisHint,
            handedness);
    }

    /// <summary>
    /// Attempts to construct an optical element frame.
    /// </summary>
    public static bool TryCreateElementFrame(
        CartesianCoordinate3 origin,
        Vector3<double> localZAxis,
        Vector3<double> localXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame) {
        return TryCreateElementFrame(
            origin,
            localZAxis,
            localXAxisHint,
            handedness,
            out frame,
            out _);
    }

    /// <summary>
    /// Constructs an optical surface frame with local Z along the surface
    /// normal.
    /// </summary>
    /// <remarks>
    /// The normal is used exactly as supplied. In particular, the factory does
    /// not silently flip it toward an incoming ray or the global positive Z
    /// axis. The caller chooses the surface orientation required by its optical
    /// model.
    /// </remarks>
    public static CoordinateFrame3 CreateSurfaceFrame(
        CartesianCoordinate3 origin,
        Vector3<double> surfaceNormal,
        Vector3<double> localXAxisHint,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        return CoordinateFrame3.CreateFromZAxis(
            origin,
            surfaceNormal,
            localXAxisHint,
            handedness);
    }

    /// <summary>
    /// Attempts to construct an optical surface frame.
    /// </summary>
    public static bool TryCreateSurfaceFrame(
        CartesianCoordinate3 origin,
        Vector3<double> surfaceNormal,
        Vector3<double> localXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame) {
        return TryCreateSurfaceFrame(
            origin,
            surfaceNormal,
            localXAxisHint,
            handedness,
            out frame,
            out _);
    }

    /// <summary>
    /// Constructs a ray-local frame with local Z along the propagation
    /// direction.
    /// </summary>
    public static CoordinateFrame3 CreateRayFrame(
        OpticalRay ray,
        Vector3<double> transverseXAxisHint,
        CoordinateHandedness handedness = CoordinateHandedness.RightHanded) {
        return ray.CreateLocalFrame(transverseXAxisHint, handedness);
    }

    public static CartesianCoordinate3 PointToGlobal(
        CartesianCoordinate3 localPoint,
        CoordinateFrame3 localFrame) {
        return localFrame.PointToReference(localPoint);
    }

    public static CartesianCoordinate3 PointFromGlobal(
        CartesianCoordinate3 globalPoint,
        CoordinateFrame3 localFrame) {
        return localFrame.PointFromReference(globalPoint);
    }

    public static Vector3<double> DirectionToGlobal(
        Vector3<double> localDirection,
        CoordinateFrame3 localFrame) {
        return localFrame.DirectionToReference(localDirection);
    }

    public static Vector3<double> DirectionFromGlobal(
        Vector3<double> globalDirection,
        CoordinateFrame3 localFrame) {
        return localFrame.DirectionFromReference(globalDirection);
    }

    public static OpticalRay RayToGlobal(
        OpticalRay localRay,
        CoordinateFrame3 localFrame) {
        return localRay.Transform(localFrame, Global);
    }

    public static OpticalRay RayFromGlobal(
        OpticalRay globalRay,
        CoordinateFrame3 localFrame) {
        return globalRay.Transform(Global, localFrame);
    }

    public static CartesianCoordinate3 TransformPoint(
        CartesianCoordinate3 point,
        CoordinateFrame3 source,
        CoordinateFrame3 target) {
        return CoordinateFrame3.TransformPoint(point, source, target);
    }

    public static Vector3<double> TransformDirection(
        Vector3<double> direction,
        CoordinateFrame3 source,
        CoordinateFrame3 target) {
        return CoordinateFrame3.TransformDirection(direction, source, target);
    }

    public static CoordinateFrame3 TransformFrame(
        CoordinateFrame3 frame,
        CoordinateFrame3 source,
        CoordinateFrame3 target) {
        return CoordinateFrame3.TransformFrame(frame, source, target);
    }

    public static OpticalRay TransformRay(
        OpticalRay ray,
        CoordinateFrame3 source,
        CoordinateFrame3 target) {
        return ray.Transform(source, target);
    }

    /// <summary>
    /// Attempts to construct an optical element frame and reports invalid input,
    /// degenerate axes, or a non-finite result.
    /// </summary>
    public static bool TryCreateElementFrame(
        CartesianCoordinate3 origin,
        Vector3<double> localZAxis,
        Vector3<double> localXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame,
        out CoordinateOperationReport report) {
        return CoordinateFrame3.TryCreateFromZAxis(
            origin,
            localZAxis,
            localXAxisHint,
            handedness,
            out frame,
            out report);
    }

    /// <summary>
    /// Attempts to construct an optical surface frame and reports invalid input,
    /// degenerate axes, or a non-finite result.
    /// </summary>
    public static bool TryCreateSurfaceFrame(
        CartesianCoordinate3 origin,
        Vector3<double> surfaceNormal,
        Vector3<double> localXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame,
        out CoordinateOperationReport report) {
        return CoordinateFrame3.TryCreateFromZAxis(
            origin,
            surfaceNormal,
            localXAxisHint,
            handedness,
            out frame,
            out report);
    }

    /// <summary>
    /// Attempts to construct a ray-local frame and reports the failing stage.
    /// </summary>
    public static bool TryCreateRayFrame(
        OpticalRay ray,
        Vector3<double> transverseXAxisHint,
        CoordinateHandedness handedness,
        out CoordinateFrame3 frame,
        out OpticsOperationReport report) {
        return ray.TryCreateLocalFrame(
            transverseXAxisHint,
            handedness,
            out frame,
            out report);
    }

    /// <summary>
    /// Attempts to transform a local point to the global optical frame.
    /// </summary>
    public static bool TryPointToGlobal(
        CartesianCoordinate3 localPoint,
        CoordinateFrame3 localFrame,
        out CartesianCoordinate3 result,
        out CoordinateOperationReport report) {
        return localFrame.TryPointToReference(
            localPoint,
            out result,
            out report);
    }

    /// <summary>
    /// Attempts to transform a global point to a local optical frame.
    /// </summary>
    public static bool TryPointFromGlobal(
        CartesianCoordinate3 globalPoint,
        CoordinateFrame3 localFrame,
        out CartesianCoordinate3 result,
        out CoordinateOperationReport report) {
        return localFrame.TryPointFromReference(
            globalPoint,
            out result,
            out report);
    }

    /// <summary>
    /// Attempts to transform a local direction to the global optical frame.
    /// </summary>
    public static bool TryDirectionToGlobal(
        Vector3<double> localDirection,
        CoordinateFrame3 localFrame,
        out Vector3<double> result,
        out CoordinateOperationReport report) {
        return localFrame.TryDirectionToReference(
            localDirection,
            out result,
            out report);
    }

    /// <summary>
    /// Attempts to transform a global direction to a local optical frame.
    /// </summary>
    public static bool TryDirectionFromGlobal(
        Vector3<double> globalDirection,
        CoordinateFrame3 localFrame,
        out Vector3<double> result,
        out CoordinateOperationReport report) {
        return localFrame.TryDirectionFromReference(
            globalDirection,
            out result,
            out report);
    }

    /// <summary>
    /// Attempts to transform a local ray to the global optical frame and reports
    /// the failing stage.
    /// </summary>
    public static bool TryRayToGlobal(
        OpticalRay localRay,
        CoordinateFrame3 localFrame,
        out OpticalRay ray,
        out OpticsOperationReport report) {
        return localRay.TryTransform(localFrame, Global, out ray, out report);
    }

    /// <summary>
    /// Attempts to transform a global ray to a local optical frame and reports
    /// the failing stage.
    /// </summary>
    public static bool TryRayFromGlobal(
        OpticalRay globalRay,
        CoordinateFrame3 localFrame,
        out OpticalRay ray,
        out OpticsOperationReport report) {
        return globalRay.TryTransform(Global, localFrame, out ray, out report);
    }

    /// <summary>
    /// Attempts to transform a point between two optical coordinate frames.
    /// </summary>
    public static bool TryTransformPoint(
        CartesianCoordinate3 point,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out CartesianCoordinate3 result,
        out CoordinateOperationReport report) {
        return CoordinateFrame3.TryTransformPoint(
            point,
            source,
            target,
            out result,
            out report);
    }

    /// <summary>
    /// Attempts to rotate a direction between two optical coordinate frames.
    /// </summary>
    public static bool TryTransformDirection(
        Vector3<double> direction,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out Vector3<double> result,
        out CoordinateOperationReport report) {
        return CoordinateFrame3.TryTransformDirection(
            direction,
            source,
            target,
            out result,
            out report);
    }

    /// <summary>
    /// Attempts to transform a frame pose between two optical coordinate frames.
    /// </summary>
    public static bool TryTransformFrame(
        CoordinateFrame3 frame,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out CoordinateFrame3 result,
        out CoordinateOperationReport report) {
        return CoordinateFrame3.TryTransformFrame(
            frame,
            source,
            target,
            out result,
            out report);
    }

    /// <summary>
    /// Attempts to transform a ray between two optical coordinate frames and
    /// reports the failing stage.
    /// </summary>
    public static bool TryTransformRay(
        OpticalRay ray,
        CoordinateFrame3 source,
        CoordinateFrame3 target,
        out OpticalRay result,
        out OpticsOperationReport report) {
        return ray.TryTransform(source, target, out result, out report);
    }
}
