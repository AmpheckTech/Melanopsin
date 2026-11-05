namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// Resolves and applies a chain of registered geodetic coordinate operations.
/// </summary>
/// <remarks>
/// <para>
/// The solver builds a directed graph from the registry. Invertible operations
/// also contribute a reverse edge. Breadth-first search minimizes the number
/// of operation steps and therefore prefers a direct operation over an
/// indirect chain. Operations registered earlier win when two paths have the
/// same length.
/// </para>
/// <para>
/// No operation is selected by geographic area, coordinate epoch, accuracy, or
/// authority preference. Callers should register only applicable operations,
/// or build separate registries for different regions and epochs. A missing
/// path is reported as failure rather than filled with an identity or guessed
/// transformation.
/// </para>
/// </remarks>
public sealed class GeodeticTransformSolver {
    private readonly GeodeticCrsRegistry _registry;
    private readonly Dictionary<
        CoordinateReferenceSystemId,
        List<GeodeticCoordinateOperationStep>> _adjacency = [];

    /// <summary>
    /// Initializes a solver over an immutable coordinate system registry.
    /// </summary>
    public GeodeticTransformSolver(GeodeticCrsRegistry registry) {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        foreach (var operation in registry.Operations) {
            AddEdge(
                operation.Source.Id,
                new GeodeticCoordinateOperationStep(operation, false));
            if (operation.IsInvertible) {
                AddEdge(
                    operation.Target.Id,
                    new GeodeticCoordinateOperationStep(operation, true));
            }
        }
    }

    /// <summary>
    /// Attempts to transform a coordinate and returns the resolved operation
    /// steps.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a path exists; otherwise,
    /// <see langword="false"/> with an empty step list.
    /// </returns>
    public bool TryTransform(
        CoordinateReferenceSystemId source,
        CoordinateReferenceSystemId target,
        GeodeticCoordinate coordinate,
        out GeodeticCoordinate result,
        out IReadOnlyList<GeodeticCoordinateOperationStep> steps) {
        return TryTransform(
            source,
            target,
            coordinate,
            out result,
            out steps,
            out _);
    }

    /// <summary>
    /// Attempts to resolve and apply a coordinate operation path and reports
    /// endpoint, path, invertibility, and numerical termination states.
    /// </summary>
    /// <param name="source">The source coordinate reference system.</param>
    /// <param name="target">The target coordinate reference system.</param>
    /// <param name="coordinate">The coordinate to transform.</param>
    /// <param name="result">Receives the transformed coordinate on success.</param>
    /// <param name="steps">Receives the resolved path in execution order.</param>
    /// <param name="report">Receives path counts and the termination status.</param>
    /// <returns><see langword="true"/> only when the transformation succeeds.</returns>
    /// <remarks>
    /// Unknown endpoints and a missing path are expected resolution failures,
    /// not identity fallbacks. Operational input and numerical failures are
    /// reported explicitly. Exceptions that indicate programming errors are not
    /// swallowed.
    /// </remarks>
    public bool TryTransform(
        CoordinateReferenceSystemId source,
        CoordinateReferenceSystemId target,
        GeodeticCoordinate coordinate,
        out GeodeticCoordinate result,
        out IReadOnlyList<GeodeticCoordinateOperationStep> steps,
        out CoordinateOperationReport report) {
        result = coordinate;
        steps = Array.Empty<GeodeticCoordinateOperationStep>();
        if (!TryFindPath(
                source,
                target,
                out var path,
                out var pathReport)) {
            result = default;
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticTransform,
                pathReport.Status,
                pathReport.DirectStepCount,
                pathReport.InverseStepCount);
            return false;
        }

        var directStepCount = 0;
        var inverseStepCount = 0;
        var transformed = coordinate;
        for (var index = 0; index < path.Count; index++) {
            var step = path[index];
            var stepSucceeded = step.IsInverse
                ? step.Operation.TryTransformInverse(
                    transformed,
                    out transformed,
                    out var stepReport)
                : step.Operation.TryTransform(
                    transformed,
                    out transformed,
                    out stepReport);
            if (!stepSucceeded) {
                result = default;
                report = new CoordinateOperationReport(
                    CoordinateOperation.GeodeticTransform,
                    stepReport.Status,
                    directStepCount,
                    inverseStepCount);
                return false;
            }

            if (step.IsInverse) {
                inverseStepCount++;
            } else {
                directStepCount++;
            }
        }

        result = transformed;
        steps = path;
        report = new CoordinateOperationReport(
            CoordinateOperation.GeodeticTransform,
            CoordinateOperationStatus.Succeeded,
            directStepCount,
            inverseStepCount);
        return true;
    }

    /// <summary>
    /// Transforms a coordinate or throws when no registered path exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No path connects the source and target coordinate reference systems.
    /// </exception>
    public GeodeticCoordinate Transform(
        CoordinateReferenceSystemId source,
        CoordinateReferenceSystemId target,
        GeodeticCoordinate coordinate) {
        if (!TryTransform(source, target, coordinate, out var result, out _)) {
            throw new InvalidOperationException(
                $"No coordinate operation path connects '{source}' to '{target}'.");
        }

        return result;
    }

    /// <summary>
    /// Resolves the shortest registered operation path without applying it.
    /// </summary>
    public bool TryFindPath(
        CoordinateReferenceSystemId source,
        CoordinateReferenceSystemId target,
        out IReadOnlyList<GeodeticCoordinateOperationStep> steps) {
        return TryFindPath(source, target, out steps, out _);
    }

    /// <summary>
    /// Resolves the shortest registered operation path and reports endpoint and
    /// connectivity failures.
    /// </summary>
    public bool TryFindPath(
        CoordinateReferenceSystemId source,
        CoordinateReferenceSystemId target,
        out IReadOnlyList<GeodeticCoordinateOperationStep> steps,
        out CoordinateOperationReport report) {
        steps = Array.Empty<GeodeticCoordinateOperationStep>();
        if (!_registry.TryGet(source, out _)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticPathResolution,
                CoordinateOperationStatus.UnknownSource);
            return false;
        }

        if (!_registry.TryGet(target, out _)) {
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticPathResolution,
                CoordinateOperationStatus.UnknownTarget);
            return false;
        }

        if (source == target) {
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticPathResolution,
                CoordinateOperationStatus.Succeeded);
            return true;
        }

        var queue = new Queue<CoordinateReferenceSystemId>();
        var visited = new HashSet<CoordinateReferenceSystemId> { source };
        var previous = new Dictionary<
            CoordinateReferenceSystemId,
            PreviousStep>();
        queue.Enqueue(source);
        var found = false;
        while (queue.Count > 0) {
            var current = queue.Dequeue();
            if (!_adjacency.TryGetValue(current, out var outgoing)) {
                continue;
            }

            foreach (var step in outgoing) {
                var next = step.IsInverse
                    ? step.Operation.Source.Id
                    : step.Operation.Target.Id;
                if (!visited.Add(next)) {
                    continue;
                }

                previous.Add(next, new PreviousStep(current, step));
                if (next == target) {
                    found = true;
                    break;
                }

                queue.Enqueue(next);
            }

            if (found) {
                break;
            }
        }

        if (!found) {
            report = new CoordinateOperationReport(
                CoordinateOperation.GeodeticPathResolution,
                CoordinateOperationStatus.NoPath);
            return false;
        }

        var path = new List<GeodeticCoordinateOperationStep>();
        var cursor = target;
        while (cursor != source) {
            var entry = previous[cursor];
            path.Add(entry.Step);
            cursor = entry.Previous;
        }

        path.Reverse();
        steps = path.AsReadOnly();
        var directStepCount = 0;
        var inverseStepCount = 0;
        foreach (var step in path) {
            if (step.IsInverse) {
                inverseStepCount++;
            } else {
                directStepCount++;
            }
        }

        report = new CoordinateOperationReport(
            CoordinateOperation.GeodeticPathResolution,
            CoordinateOperationStatus.Succeeded,
            directStepCount,
            inverseStepCount);
        return true;
    }

    private void AddEdge(
        CoordinateReferenceSystemId source,
        GeodeticCoordinateOperationStep step) {
        if (!_adjacency.TryGetValue(source, out var outgoing)) {
            outgoing = [];
            _adjacency.Add(source, outgoing);
        }

        outgoing.Add(step);
    }

    private readonly record struct PreviousStep(
        CoordinateReferenceSystemId Previous,
        GeodeticCoordinateOperationStep Step);
}
