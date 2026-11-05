// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// A two-dimensional ray <c>origin + t * direction</c> with <c>t &gt;= 0</c>.
/// </summary>
/// <remarks>
/// Direction is not required to be normalized, so intersection distances are
/// expressed in units of the supplied direction vector.
/// </remarks>
public readonly partial record struct Ray2 {
    public Ray2(Point origin, Vector2 direction) {
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y) ||
            !direction.IsFinite) {
            throw new ArgumentException(
                "Ray origin and direction must be finite.",
                nameof(origin));
        }

        if (direction.X == 0 && direction.Y == 0) {
            throw new ArgumentException(
                "A ray direction must be non-zero.",
                nameof(direction));
        }

        Origin = origin;
        Direction = direction;
    }

    public Point Origin { get; }

    public Vector2 Direction { get; }

    public Point GetPoint(float distance) {
        return Origin + (Direction * distance);
    }

    public bool TryIntersect(
        Aabb2 bounds,
        out float nearDistance,
        out float farDistance) {
        return TryIntersect(
            bounds,
            out nearDistance,
            out farDistance,
            out _);
    }

    /// <summary>
    /// Attempts to intersect a closed bounding box and reports mathematical
    /// absence separately from numerical failure.
    /// </summary>
    public bool TryIntersect(
        Aabb2 bounds,
        out float nearDistance,
        out float farDistance,
        out GeometryOperationReport report) {
        var near = double.NegativeInfinity;
        var far = double.PositiveInfinity;
        if (!UpdateSlab(
            Origin.X,
            Direction.X,
            bounds.Minimum.X,
            bounds.Maximum.X,
            ref near,
            ref far) ||
            !UpdateSlab(
                Origin.Y,
                Direction.Y,
                bounds.Minimum.Y,
                bounds.Maximum.Y,
                ref near,
                ref far)) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayBoundsIntersection,
                GeometryStatus.NoIntersection);
            return false;
        }

        near = Math.Max(near, 0);
        if (!double.IsFinite(near) || !double.IsFinite(far)) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayBoundsIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (near > far) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayBoundsIntersection,
                far < 0 ? GeometryStatus.BehindOrigin : GeometryStatus.NoIntersection);
            return false;
        }

        if (!MatrixNumerics.IsRepresentableAsFloat(near) ||
            !MatrixNumerics.IsRepresentableAsFloat(far)) {
            nearDistance = float.NaN;
            farDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RayBoundsIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        nearDistance = (float)near;
        farDistance = (float)far;
        report = new GeometryOperationReport(
            GeometryOperation.RayBoundsIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    public bool TryIntersect(Segment2 segment, out float rayDistance) {
        return TryIntersect(segment, out rayDistance, out _);
    }

    /// <summary>
    /// Attempts to intersect a closed segment and reports whether the
    /// intersection is parallel, behind the ray, or outside the segment.
    /// </summary>
    public bool TryIntersect(
        Segment2 segment,
        out float rayDistance,
        out GeometryOperationReport report) {
        var offsetX = (double)segment.Start.X - Origin.X;
        var offsetY = (double)segment.Start.Y - Origin.Y;
        var denominator = GeometryPredicates.Cross(Direction, segment.Direction);
        if (denominator == 0) {
            rayDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySegmentIntersection,
                segment.Direction.X == 0 && segment.Direction.Y == 0
                    ? GeometryStatus.DegenerateGeometry
                    : GeometryStatus.Parallel);
            return false;
        }

        var rayResult = ((offsetX * segment.Direction.Y) -
            (offsetY * segment.Direction.X)) / denominator;
        var segmentAmount = ((offsetX * Direction.Y) -
            (offsetY * Direction.X)) / denominator;
        if (!double.IsFinite(rayResult) || !double.IsFinite(segmentAmount)) {
            rayDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySegmentIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (rayResult < 0) {
            rayDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySegmentIntersection,
                GeometryStatus.BehindOrigin);
            return false;
        }

        if (segmentAmount < 0 || segmentAmount > 1) {
            rayDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySegmentIntersection,
                GeometryStatus.OutsideBounds);
            return false;
        }

        if (!MatrixNumerics.IsRepresentableAsFloat(rayResult)) {
            rayDistance = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.RaySegmentIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        rayDistance = (float)rayResult;
        report = new GeometryOperationReport(
            GeometryOperation.RaySegmentIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    private static bool UpdateSlab(
        float origin,
        float direction,
        float minimum,
        float maximum,
        ref double near,
        ref double far) {
        if (direction == 0) {
            return origin >= minimum && origin <= maximum;
        }

        var first = ((double)minimum - origin) / direction;
        var second = ((double)maximum - origin) / direction;
        if (first > second) {
            (first, second) = (second, first);
        }

        near = Math.Max(near, first);
        far = Math.Min(far, second);
        return near <= far;
    }
}
