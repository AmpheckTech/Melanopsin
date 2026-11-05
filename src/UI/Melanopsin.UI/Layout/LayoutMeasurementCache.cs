namespace Melanopsin.UI.Layout;

internal sealed class LayoutMeasurementCache {
    // Resize creates a fresh set of constraints on every configure event. Keep
    // a few recent variants per node so alternating or repeated sizes reuse
    // measurements without retaining an unbounded global constraint history.
    private const int MaxPersistentConstraintsPerNode = 4;

    private readonly Dictionary<MeasurementKey, PassMeasurement> _passMeasurements = [];
    private readonly Dictionary<Node, NodeMeasurementCache> _persistentMeasurements =
        new(ReferenceEqualityComparer.Instance);

    public void BeginPass(bool clearPersistentMeasurements) {
        _passMeasurements.Clear();
        if (clearPersistentMeasurements) {
            _persistentMeasurements.Clear();
        }
    }

    public bool TryGetPassMeasurement(
        MeasurementKey key,
        out LayoutSize size,
        out bool usesViewportUnits) {
        if (_passMeasurements.TryGetValue(key, out var cached)) {
            size = cached.Size;
            usesViewportUnits = cached.UsesViewportUnits;
            return true;
        }

        size = default;
        usesViewportUnits = false;
        return false;
    }

    public bool TryGetPersistentMeasurement(
        Node node,
        long styleSheetVersion,
        LayoutConstraints constraints,
        UiMetrics metrics,
        out LayoutSize size,
        out bool usesViewportUnits) {
        if (_persistentMeasurements.TryGetValue(node, out var measurements) &&
            measurements.TryGet(
                node,
                styleSheetVersion,
                constraints,
                metrics,
                out var cached)) {
            size = cached.Size;
            usesViewportUnits = cached.UsesViewportUnits;
            return true;
        }

        size = default;
        usesViewportUnits = false;
        return false;
    }

    public void Store(
        MeasurementKey key,
        LayoutSize size,
        long layoutRevision,
        long styleSheetVersion,
        LayoutConstraints constraints,
        UiMetrics metrics,
        bool usesViewportUnits) {
        _passMeasurements[key] = new PassMeasurement(size, usesViewportUnits);
        if (!_persistentMeasurements.TryGetValue(key.Node, out var measurements)) {
            measurements = new NodeMeasurementCache();
            _persistentMeasurements.Add(key.Node, measurements);
        }

        measurements.Store(
            key.Node,
            size,
            layoutRevision,
            styleSheetVersion,
            constraints,
            metrics,
            usesViewportUnits);
    }

    public void StorePassMeasurement(
        MeasurementKey key,
        LayoutSize size,
        bool usesViewportUnits) {
        _passMeasurements[key] = new PassMeasurement(size, usesViewportUnits);
    }

    public void Prune(LayoutSnapshot snapshot) {
        if (_persistentMeasurements.Count == 0) {
            return;
        }

        List<Node>? stale = null;
        foreach (var node in _persistentMeasurements.Keys) {
            if (snapshot.Contains(node)) {
                continue;
            }

            stale ??= [];
            stale.Add(node);
        }

        if (stale is null) {
            return;
        }

        foreach (var node in stale) {
            _persistentMeasurements.Remove(node);
        }
    }

    private readonly record struct PassMeasurement(
        LayoutSize Size,
        bool UsesViewportUnits);

    private sealed class NodeMeasurementCache {
        private readonly List<CachedMeasurement> _measurements = [];

        public bool TryGet(
            Node node,
            long styleSheetVersion,
            LayoutConstraints constraints,
            UiMetrics metrics,
            out CachedMeasurement measurement) {
            for (var index = 0; index < _measurements.Count; index++) {
                var candidate = _measurements[index];
                if (!candidate.IsValid(
                        node,
                        styleSheetVersion,
                        constraints,
                        metrics)) {
                    continue;
                }

                if (index != 0) {
                    _measurements.RemoveAt(index);
                    _measurements.Insert(0, candidate);
                }

                measurement = candidate;
                return true;
            }

            measurement = default;
            return false;
        }

        public void Store(
            Node node,
            LayoutSize size,
            long layoutRevision,
            long styleSheetVersion,
            LayoutConstraints constraints,
            UiMetrics metrics,
            bool usesViewportUnits) {
            var replacement = new CachedMeasurement(
                layoutRevision,
                styleSheetVersion,
                constraints,
                metrics,
                usesViewportUnits,
                size);

            for (var index = _measurements.Count - 1; index >= 0; index--) {
                var existing = _measurements[index];
                if (!existing.IsValid(
                        node,
                        styleSheetVersion,
                        constraints,
                        metrics)) {
                    _measurements.RemoveAt(index);
                }
            }

            _measurements.Insert(0, replacement);
            if (_measurements.Count > MaxPersistentConstraintsPerNode) {
                _measurements.RemoveRange(
                    MaxPersistentConstraintsPerNode,
                    _measurements.Count - MaxPersistentConstraintsPerNode);
            }
        }
    }
}
