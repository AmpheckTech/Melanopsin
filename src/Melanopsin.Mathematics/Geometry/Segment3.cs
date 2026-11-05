namespace Melanopsin.Mathematics;

/// <summary>
/// A closed line segment in three-dimensional space.
/// </summary>
public readonly partial record struct Segment3 {
    public Segment3(Vector3 start, Vector3 end) {
        if (!start.IsFinite || !end.IsFinite) {
            throw new ArgumentException(
                "Segment endpoints must be finite.",
                nameof(start));
        }

        Start = start;
        End = end;
    }

    public Vector3 Start { get; }

    public Vector3 End { get; }

    public Vector3 Direction => End - Start;

    public float LengthSquared => (float)GeometryPredicates.LengthSquared(Direction);

    public float Length => (float)GeometryPredicates.Length(Direction);

    public Vector3 GetPoint(float amount) {
        return Start + (Direction * amount);
    }

    public Vector3 ClosestPoint(Vector3 point) {
        double directionX = (double)End.X - Start.X;
        double directionY = (double)End.Y - Start.Y;
        double directionZ = (double)End.Z - Start.Z;
        var lengthSquared = (directionX * directionX) +
            (directionY * directionY) +
            (directionZ * directionZ);
        if (lengthSquared == 0) {
            return Start;
        }

        var amount = ScalarMath.Clamp(
            ((((double)point.X - Start.X) * directionX) +
                (((double)point.Y - Start.Y) * directionY) +
                (((double)point.Z - Start.Z) * directionZ)) / lengthSquared,
            0,
            1);
        return new Vector3(
            (float)(Start.X + (amount * directionX)),
            (float)(Start.Y + (amount * directionY)),
            (float)(Start.Z + (amount * directionZ)));
    }

    public float DistanceSquared(Vector3 point) {
        return (float)GeometryPredicates.DistanceSquared(point, ClosestPoint(point));
    }

    public float Distance(Vector3 point) {
        return (float)GeometryPredicates.Distance(point, ClosestPoint(point));
    }

    /// <summary>
    /// Finds the closest point pair between two closed segments.
    /// </summary>
    public static float ClosestPoints(
        Segment3 first,
        Segment3 second,
        out Vector3 firstPoint,
        out Vector3 secondPoint) {
        double firstDirectionX = (double)first.End.X - first.Start.X;
        double firstDirectionY = (double)first.End.Y - first.Start.Y;
        double firstDirectionZ = (double)first.End.Z - first.Start.Z;
        double secondDirectionX = (double)second.End.X - second.Start.X;
        double secondDirectionY = (double)second.End.Y - second.Start.Y;
        double secondDirectionZ = (double)second.End.Z - second.Start.Z;
        double firstLengthSquared = (firstDirectionX * firstDirectionX) +
            (firstDirectionY * firstDirectionY) +
            (firstDirectionZ * firstDirectionZ);
        double secondLengthSquared = (secondDirectionX * secondDirectionX) +
            (secondDirectionY * secondDirectionY) +
            (secondDirectionZ * secondDirectionZ);
        double offsetX = (double)first.Start.X - second.Start.X;
        double offsetY = (double)first.Start.Y - second.Start.Y;
        double offsetZ = (double)first.Start.Z - second.Start.Z;
        var mixed = (firstDirectionX * secondDirectionX) +
            (firstDirectionY * secondDirectionY) +
            (firstDirectionZ * secondDirectionZ);
        var firstOffset = (firstDirectionX * offsetX) +
            (firstDirectionY * offsetY) +
            (firstDirectionZ * offsetZ);
        var secondOffset = (secondDirectionX * offsetX) +
            (secondDirectionY * offsetY) +
            (secondDirectionZ * offsetZ);
        double firstAmount;
        double secondAmount;
        if (firstLengthSquared == 0 && secondLengthSquared == 0) {
            firstAmount = 0;
            secondAmount = 0;
        } else if (firstLengthSquared == 0) {
            firstAmount = 0;
            secondAmount = ScalarMath.Clamp(secondOffset / secondLengthSquared, 0, 1);
        } else {
            if (secondLengthSquared == 0) {
                secondAmount = 0;
                firstAmount = ScalarMath.Clamp(-firstOffset / firstLengthSquared, 0, 1);
            } else {
                var denominator = (firstLengthSquared * secondLengthSquared) - (mixed * mixed);
                firstAmount = denominator == 0
                    ? 0
                    : ScalarMath.Clamp(
                        ((mixed * secondOffset) - (firstOffset * secondLengthSquared)) /
                            denominator,
                        0,
                        1);
                secondAmount =
                    ((mixed * firstAmount) + secondOffset) / secondLengthSquared;
                if (secondAmount < 0) {
                    secondAmount = 0;
                    firstAmount = ScalarMath.Clamp(-firstOffset / firstLengthSquared, 0, 1);
                } else if (secondAmount > 1) {
                    secondAmount = 1;
                    firstAmount = ScalarMath.Clamp(
                        (mixed - firstOffset) / firstLengthSquared,
                        0,
                        1);
                }
            }
        }

        firstPoint = new Vector3(
            (float)(first.Start.X + (firstAmount * firstDirectionX)),
            (float)(first.Start.Y + (firstAmount * firstDirectionY)),
            (float)(first.Start.Z + (firstAmount * firstDirectionZ)));
        secondPoint = new Vector3(
            (float)(second.Start.X + (secondAmount * secondDirectionX)),
            (float)(second.Start.Y + (secondAmount * secondDirectionY)),
            (float)(second.Start.Z + (secondAmount * secondDirectionZ)));
        return (float)GeometryPredicates.Distance(firstPoint, secondPoint);
    }
}
