// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers;

namespace Melanopsin.Mathematics;

public sealed partial class Polygon2 {
    /// <summary>
    /// Clips this polygon against a convex clipping window.
    /// </summary>
    /// <param name="window">
    /// A simple, non-degenerate convex polygon with either winding direction.
    /// </param>
    /// <returns>
    /// The boundary of the closed intersection in input-window edge order. The
    /// result may contain zero, one, or two vertices for an empty, touching, or
    /// collinear intersection; it is not wrapped in a <see cref="Polygon2"/>
    /// because such a result need not have three distinct vertices.
    /// </returns>
    /// <remarks>
    /// This is the Sutherland-Hodgman algorithm (1974),
    /// <see href="https://doi.org/10.1145/360767.360802">doi:10.1145/360767.360802</see>.
    /// This polygon may be concave. Explicit tolerances are not applied:
    /// callers requiring robust near-degenerate predicates should snap or
    /// normalize coordinates first. The clipping window is validated for
    /// simplicity and convexity.
    /// </remarks>
    public Vector2[] ClipToConvex(Polygon2 window) {
        if (!TryClipToConvex(window, out var result, out var report)) {
            throw new ArgumentException(
                $"The clipping window is invalid: {report.Status}.",
                nameof(window));
        }

        return result;
    }

    /// <summary>
    /// Attempts to clip this polygon against a convex window and reports an
    /// invalid window instead of throwing.
    /// </summary>
    /// <remarks>
    /// A successful operation may return an empty array when the polygons do
    /// not overlap. An empty array is a valid clipped boundary, not a failure.
    /// </remarks>
    public bool TryClipToConvex(
        Polygon2 window,
        out Vector2[] result,
        out GeometryOperationReport report) {
        if (window is null) {
            result = [];
            report = new GeometryOperationReport(
                GeometryOperation.PolygonClipping,
                GeometryStatus.InvalidInput);
            return false;
        }

        if (!IsValidClipWindow(window)) {
            result = [];
            report = new GeometryOperationReport(
                GeometryOperation.PolygonClipping,
                GeometryStatus.InvalidInput);
            return false;
        }

        if (!TryGetClippingCapacity(window, out var capacity)) {
            result = [];
            report = new GeometryOperationReport(
                GeometryOperation.PolygonClipping,
                GeometryStatus.InvalidInput);
            return false;
        }

        var workspace = ArrayPool<Vector2>.Shared.Rent(
            checked(capacity * 2));
        try {
            if (!TryClipToConvexCore(
                    window,
                    workspace.AsSpan(0, capacity * 2),
                    out var clipped,
                    out report)) {
                result = [];
                return false;
            }

            result = clipped.ToArray();
            return true;
        } finally {
            ArrayPool<Vector2>.Shared.Return(
                workspace,
                clearArray: true);
        }
    }

    /// <summary>
    /// Attempts to clip this polygon without allocating a result array.
    /// </summary>
    /// <param name="window">The convex clipping window.</param>
    /// <param name="workspace">
    /// A caller-owned buffer with at least twice
    /// <c>VertexCount + window.VertexCount</c> elements. The result aliases its
    /// prefix until the buffer is next modified.
    /// </param>
    /// <param name="result">The clipped boundary stored at the start of <paramref name="workspace"/>.</param>
    /// <param name="report">The operation report.</param>
    /// <remarks>
    /// An empty result is successful and represented by an empty span. An
    /// insufficient workspace is a programming error and throws
    /// <see cref="ArgumentException"/>; an invalid clipping window returns
    /// <see langword="false"/>.
    /// </remarks>
    public bool TryClipToConvex(
        Polygon2 window,
        Span<Vector2> workspace,
        out ReadOnlySpan<Vector2> result,
        out GeometryOperationReport report) {
        result = default;
        if (window is null || !IsValidClipWindow(window)) {
            report = new GeometryOperationReport(
                GeometryOperation.PolygonClipping,
                GeometryStatus.InvalidInput);
            return false;
        }

        if (!TryGetClippingCapacity(window, out var capacity)) {
            report = new GeometryOperationReport(
                GeometryOperation.PolygonClipping,
                GeometryStatus.InvalidInput);
            return false;
        }

        if (workspace.Length < capacity * 2L) {
            throw new ArgumentException(
                $"Clipping requires at least {capacity * 2L} elements.",
                nameof(workspace));
        }

        if (!TryClipToConvexCore(
                window,
                workspace[..(capacity * 2)],
                out var clipped,
                out report)) {
            return false;
        }

        clipped.CopyTo(workspace);
        result = workspace[..clipped.Length];
        return true;
    }

    private bool TryClipToConvexCore(
        Polygon2 window,
        Span<Vector2> workspace,
        out ReadOnlySpan<Vector2> result,
        out GeometryOperationReport report) {
        var capacity = workspace.Length / 2;
        var input = workspace[..capacity];
        var output = workspace[capacity..];
        _vertices.CopyTo(input);
        var inputLength = _vertices.Length;
        var orientation = Math.Sign(window.ComputeTwiceSignedArea());
        for (var edgeIndex = 0;
            edgeIndex < window._vertices.Length && inputLength > 0;
            edgeIndex++) {
            var clipStart = window._vertices[edgeIndex];
            var clipEnd = window._vertices[
                (edgeIndex + 1) % window._vertices.Length];
            var outputLength = 0;
            var previous = input[inputLength - 1];
            var previousInside = IsInside(
                previous,
                clipStart,
                clipEnd,
                orientation);
            for (var index = 0; index < inputLength; index++) {
                var current = input[index];
                var currentInside = IsInside(
                    current,
                    clipStart,
                    clipEnd,
                    orientation);
                if (currentInside) {
                    if (!previousInside) {
                        output[outputLength++] = IntersectEdge(
                            previous,
                            current,
                            clipStart,
                            clipEnd);
                    }

                    output[outputLength++] = current;
                } else if (previousInside) {
                    output[outputLength++] = IntersectEdge(
                        previous,
                        current,
                        clipStart,
                        clipEnd);
                }

                previous = current;
                previousInside = currentInside;
            }

            inputLength = outputLength;
            var swap = input;
            input = output;
            output = swap;
        }

        result = input[..inputLength];
        report = new GeometryOperationReport(
            GeometryOperation.PolygonClipping,
            GeometryStatus.Succeeded);
        return true;
    }

    private bool TryGetClippingCapacity(
        Polygon2 window,
        out int capacity) {
        var required = 2L * ((long)_vertices.Length + window._vertices.Length);
        if (required > int.MaxValue) {
            capacity = 0;
            return false;
        }

        capacity = (int)(required / 2);
        return true;
    }

    private static bool IsInside(
        Vector2 point,
        Vector2 clipStart,
        Vector2 clipEnd,
        int orientation) {
        var side = GeometryPredicates.Cross(clipStart, clipEnd, point);
        return orientation > 0 ? side >= 0 : side <= 0;
    }

    private static Vector2 IntersectEdge(
        Vector2 first,
        Vector2 second,
        Vector2 clipStart,
        Vector2 clipEnd) {
        var firstDistance = GeometryPredicates.Cross(
            clipStart,
            clipEnd,
            first);
        var secondDistance = GeometryPredicates.Cross(
            clipStart,
            clipEnd,
            second);
        var denominator = firstDistance - secondDistance;
        if (denominator == 0) {
            return first;
        }

        var amount = firstDistance / denominator;
        return new Vector2(
            (float)((double)first.X + (((double)second.X - first.X) * amount)),
            (float)((double)first.Y + (((double)second.Y - first.Y) * amount)));
    }

    private static bool IsValidClipWindow(Polygon2 window) {
        var twiceSignedArea = window.ComputeTwiceSignedArea();
        if (!window.IsSimple || twiceSignedArea == 0) {
            return false;
        }

        var orientation = Math.Sign(twiceSignedArea);
        for (var index = 0; index < window._vertices.Length; index++) {
            var previous = window._vertices[
                (index + window._vertices.Length - 1) % window._vertices.Length];
            var current = window._vertices[index];
            var next = window._vertices[(index + 1) % window._vertices.Length];
            var turn = GeometryPredicates.Cross(previous, current, next);
            if (turn * orientation < 0) {
                return false;
            }
        }

        return true;
    }
}
