// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

public readonly partial record struct Segment2 {
    /// <summary>
    /// Tests intersection with a closed axis-aligned box.
    /// </summary>
    public bool Intersects(Aabb2 box) {
        return TryIntersect(box, out _, out _);
    }

    /// <summary>
    /// Computes the segment parameter interval that lies inside a closed
    /// axis-aligned box.
    /// </summary>
    /// <param name="box">The box to test.</param>
    /// <param name="entryAmount">
    /// The first parameter in <c>[0, 1]</c> inside the box.
    /// </param>
    /// <param name="exitAmount">
    /// The last parameter in <c>[0, 1]</c> inside the box.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a non-empty intersection; otherwise the
    /// output amounts are zero.
    /// </returns>
    /// <remarks>
    /// The segment is closed and box boundaries are included. A tangential
    /// intersection therefore succeeds. No epsilon is applied.
    /// </remarks>
    public bool TryIntersect(
        Aabb2 box,
        out float entryAmount,
        out float exitAmount) {
        return TryIntersect(box, out entryAmount, out exitAmount, out _);
    }

    /// <summary>
    /// Attempts to intersect a closed bounding box and reports whether the
    /// segment lies entirely outside it.
    /// </summary>
    public bool TryIntersect(
        Aabb2 box,
        out float entryAmount,
        out float exitAmount,
        out GeometryOperationReport report) {
        var minimum = 0.0;
        var maximum = 1.0;
        if (!UpdateSlab(
                Start.X,
                Direction.X,
                box.Minimum.X,
                box.Maximum.X,
                ref minimum,
                ref maximum) ||
            !UpdateSlab(
                Start.Y,
                Direction.Y,
                box.Minimum.Y,
                box.Maximum.Y,
                ref minimum,
                ref maximum)) {
            entryAmount = 0;
            exitAmount = 0;
            report = new GeometryOperationReport(
                GeometryOperation.SegmentBoundsIntersection,
                GeometryStatus.NoIntersection);
            return false;
        }

        if (!MatrixNumerics.IsRepresentableAsFloat(minimum) ||
            !MatrixNumerics.IsRepresentableAsFloat(maximum)) {
            entryAmount = float.NaN;
            exitAmount = float.NaN;
            report = new GeometryOperationReport(
                GeometryOperation.SegmentBoundsIntersection,
                GeometryStatus.NonFiniteOutput);
            return false;
        }

        entryAmount = (float)minimum;
        exitAmount = (float)maximum;
        report = new GeometryOperationReport(
            GeometryOperation.SegmentBoundsIntersection,
            GeometryStatus.Succeeded);
        return true;
    }

    private static bool UpdateSlab(
        float origin,
        float direction,
        float minimum,
        float maximum,
        ref double entry,
        ref double exit) {
        if (direction == 0) {
            return origin >= minimum && origin <= maximum;
        }

        var first = ((double)minimum - origin) / direction;
        var second = ((double)maximum - origin) / direction;
        if (first > second) {
            (first, second) = (second, first);
        }

        entry = Math.Max(entry, first);
        exit = Math.Min(exit, second);
        return entry <= exit;
    }
}
