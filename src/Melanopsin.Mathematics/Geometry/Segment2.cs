// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>A closed line segment in two-dimensional space.</summary>
public readonly partial record struct Segment2 {
    public Segment2(Point start, Point end) {
        if (!float.IsFinite(start.X) || !float.IsFinite(start.Y) ||
            !float.IsFinite(end.X) || !float.IsFinite(end.Y)) {
            throw new ArgumentException("Segment endpoints must be finite.");
        }

        Start = start;
        End = end;
    }

    public Point Start { get; }

    public Point End { get; }

    public Vector2 Direction => End - Start;

    public float LengthSquared => (float)GeometryPredicates.LengthSquared(Direction);

    public float Length => (float)GeometryPredicates.Length(Direction);

    public Point GetPoint(float amount) {
        return Start + (Direction * amount);
    }

    public Vector2 ClosestPoint(Vector2 point) {
        double directionX = (double)End.X - Start.X;
        double directionY = (double)End.Y - Start.Y;
        var lengthSquared = (directionX * directionX) + (directionY * directionY);
        if (lengthSquared == 0) {
            return new Vector2(Start.X, Start.Y);
        }

        var amount = ScalarMath.Clamp(
            ((((double)point.X - Start.X) * directionX) +
                (((double)point.Y - Start.Y) * directionY)) / lengthSquared,
            0,
            1);
        return new Vector2(
            (float)(Start.X + (amount * directionX)),
            (float)(Start.Y + (amount * directionY)));
    }

    public float DistanceSquared(Vector2 point) {
        return (float)GeometryPredicates.DistanceSquared(point, ClosestPoint(point));
    }

    public float Distance(Vector2 point) {
        return (float)GeometryPredicates.Distance(point, ClosestPoint(point));
    }

    /// <summary>
    /// Finds the closest point pair between two closed segments.
    /// </summary>
    public static float ClosestPoints(
        Segment2 first,
        Segment2 second,
        out Vector2 firstPoint,
        out Vector2 secondPoint) {
        double firstDirectionX = (double)first.End.X - first.Start.X;
        double firstDirectionY = (double)first.End.Y - first.Start.Y;
        double secondDirectionX = (double)second.End.X - second.Start.X;
        double secondDirectionY = (double)second.End.Y - second.Start.Y;
        double firstLengthSquared = (firstDirectionX * firstDirectionX) +
            (firstDirectionY * firstDirectionY);
        double secondLengthSquared = (secondDirectionX * secondDirectionX) +
            (secondDirectionY * secondDirectionY);
        double offsetX = (double)first.Start.X - second.Start.X;
        double offsetY = (double)first.Start.Y - second.Start.Y;
        var mixed = (firstDirectionX * secondDirectionX) +
            (firstDirectionY * secondDirectionY);
        var firstOffset = (firstDirectionX * offsetX) +
            (firstDirectionY * offsetY);
        var secondOffset = (secondDirectionX * offsetX) +
            (secondDirectionY * offsetY);
        double firstAmount;
        double secondAmount;
        if (firstLengthSquared == 0 && secondLengthSquared == 0) {
            firstAmount = 0;
            secondAmount = 0;
        } else if (firstLengthSquared == 0) {
            firstAmount = 0;
            secondAmount = ScalarMath.Clamp(
                secondOffset / secondLengthSquared,
                0,
                1);
        } else if (secondLengthSquared == 0) {
            secondAmount = 0;
            firstAmount = ScalarMath.Clamp(
                -firstOffset / firstLengthSquared,
                0,
                1);
        } else {
            var denominator = (firstLengthSquared * secondLengthSquared) -
                (mixed * mixed);
            firstAmount = denominator == 0
                ? 0
                : ScalarMath.Clamp(
                    ((mixed * secondOffset) -
                        (firstOffset * secondLengthSquared)) / denominator,
                    0,
                    1);
            secondAmount =
                ((mixed * firstAmount) + secondOffset) / secondLengthSquared;
            if (secondAmount < 0) {
                secondAmount = 0;
                firstAmount = ScalarMath.Clamp(
                    -firstOffset / firstLengthSquared,
                    0,
                    1);
            } else if (secondAmount > 1) {
                secondAmount = 1;
                firstAmount = ScalarMath.Clamp(
                    (mixed - firstOffset) / firstLengthSquared,
                    0,
                    1);
            }
        }

        firstPoint = new Vector2(
            (float)(first.Start.X + (firstAmount * firstDirectionX)),
            (float)(first.Start.Y + (firstAmount * firstDirectionY)));
        secondPoint = new Vector2(
            (float)(second.Start.X + (secondAmount * secondDirectionX)),
            (float)(second.Start.Y + (secondAmount * secondDirectionY)));
        return (float)GeometryPredicates.Distance(firstPoint, secondPoint);
    }

    /// <summary>
    /// Tests a proper or endpoint intersection with another segment.
    /// </summary>
    /// <remarks>
    /// Collinear overlapping segments report false because they do not have a
    /// unique intersection parameter pair. Use
    /// <see cref="ClosestPoints"/> when a distance result is needed instead.
    /// </remarks>
    public bool TryIntersect(Segment2 other, out float firstAmount) {
        return TryIntersect(other, out firstAmount, out _);
    }

    /// <summary>
    /// Attempts a unique proper or endpoint intersection and reports whether
    /// the segments are parallel, degenerate, or miss each other.
    /// </summary>
    public bool TryIntersect(
        Segment2 other,
        out float firstAmount,
        out GeometryOperationReport report) {
        var firstDirection = Direction;
        var secondDirection = other.Direction;
        var denominator = GeometryPredicates.Cross(
            firstDirection,
            secondDirection);
        if (denominator == 0) {
            firstAmount = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.SegmentIntersection,
                (firstDirection.X == 0 && firstDirection.Y == 0) ||
                    (secondDirection.X == 0 && secondDirection.Y == 0)
                    ? GeometryStatus.DegenerateGeometry
                    : GeometryStatus.Parallel);
            return false;
        }

        var offset = new Vector2(
            other.Start.X - Start.X,
            other.Start.Y - Start.Y);
        var firstResult =
            GeometryPredicates.Cross(offset, secondDirection) / denominator;
        var secondResult =
            GeometryPredicates.Cross(offset, firstDirection) / denominator;
        if (!double.IsFinite(firstResult) || !double.IsFinite(secondResult)) {
            firstAmount = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.SegmentIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        if (firstResult < 0 || firstResult > 1 ||
            secondResult < 0 || secondResult > 1) {
            firstAmount = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.SegmentIntersection,
                GeometryStatus.OutsideBounds);
            return false;
        }

        firstAmount = (float)firstResult;
        if (!float.IsFinite(firstAmount)) {
            firstAmount = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.SegmentIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        report = new GeometryOperationReport(
            GeometryOperation.SegmentIntersection,
            GeometryStatus.Succeeded);
        return true;
    }
}
