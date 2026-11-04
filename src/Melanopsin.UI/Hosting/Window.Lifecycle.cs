namespace Melanopsin.UI.Hosting;

public sealed partial class Window {
    internal void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        List<Exception>? failures = null;
        if (_root is not null) {
            try {
                _root.Invalidated -= OnRootInvalidated;
                _root.UnmountTree();
            } catch (Exception exception) {
                (failures ??= []).Add(exception);
            } finally {
                _root = null;
            }
        }

        try {
            _target.Dispose();
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            DrainRetiredTargets();
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        try {
            LifecycleEvent?.Invoke(this, new WindowLifecycleEventArgs(
                WindowLifecycleKind.Closed,
                Id,
                Size));
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }

        if (failures is not null) {
            throw new AggregateException(
                "The window failed to dispose cleanly.",
                failures);
        }
    }

    internal void DrainRetiredTargets() {
        if (_retiredTargets.Count == 0) {
            return;
        }

        List<Exception>? failures = null;
        for (var index = 0; index < _retiredTargets.Count;) {
            var target = _retiredTargets[index];
            if (target.IsFrameAvailable) {
                _retiredTargets.RemoveAt(index);
                try {
                    target.Dispose();
                } catch (Exception exception) {
                    (failures ??= []).Add(exception);
                }

                continue;
            }

            index++;
        }

        if (failures is not null) {
            throw new AggregateException(
                "One or more retired render targets failed to dispose.",
                failures);
        }
    }

    internal void RaiseLifecycle(
        WindowLifecycleKind kind,
        int x = 0,
        int y = 0) {
        LifecycleEvent?.Invoke(this, new WindowLifecycleEventArgs(
            kind,
            Id,
            Size,
            x,
            y));
    }
}
