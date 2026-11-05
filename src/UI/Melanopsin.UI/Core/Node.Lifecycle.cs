using System.Runtime.ExceptionServices;

namespace Melanopsin.UI;

public abstract partial class Node {
    /// <summary>
    /// Commits a parent-pointer change. The owning element updates its child
    /// collection first, then calls <see cref="CompleteParentChange"/> to run
    /// observable callbacks against a consistent tree.
    /// </summary>
    internal void SetParentState(Node? parent) {
        Parent = parent;
    }

    internal void CompleteParentChange() {
        OnParentChanged(Parent is not null);
        if (Parent?.IsMounted == true) {
            MountTree();
        } else {
            UnmountTree();
        }
    }

    internal void MountTree() {
        if (_isMounted) {
            return;
        }

        _isMounted = true;
        Exception? failure = null;
        CaptureException(ref failure, this, static node => node.OnMounted());
        CaptureException(ref failure, this, static node => node.Mounted?.Invoke(node));
        MountChildren(ref failure);
        ThrowIfFailed(failure);
    }

    internal void UnmountTree() {
        if (!_isMounted) {
            return;
        }

        Exception? failure = null;
        UnmountChildren(ref failure);
        _isMounted = false;
        if (_isFocused) {
            IsFocused = false;
        }

        CaptureException(ref failure, this, static node => node._mountCancellation?.Cancel());
        CaptureException(ref failure, this, static node => node.OnUnmounted());
        CaptureException(ref failure, this, static node => node.Unmounted?.Invoke(node));
        CaptureException(ref failure, this, static node => node.DisposeMountResources());
        ThrowIfFailed(failure);
    }

    internal virtual void MountChildren(ref Exception? failure) {
    }

    internal virtual void UnmountChildren(ref Exception? failure) {
    }

    protected internal virtual void OnMounted() {
    }

    protected internal virtual void OnUnmounted() {
    }

    internal virtual void OnParentChanged(bool hasParent) {
    }

    public void RegisterMountedResource(IDisposable resource) {
        ArgumentNullException.ThrowIfNull(resource);
        if (!_isMounted) {
            throw new InvalidOperationException(
                "A mounted resource can only be registered while the node is mounted.");
        }

        (_mountResources ??= []).Add(resource);
    }

    internal static void CaptureException(
        ref Exception? failure,
        Action action) {
        try {
            action();
        } catch (Exception error) {
            failure = CombineFailures(failure, error);
        }
    }

    /// <summary>
    /// Runs a callback with its state passed explicitly so callers can supply a
    /// cached static delegate instead of allocating a closure per invocation.
    /// </summary>
    internal static void CaptureException<TState>(
        ref Exception? failure,
        TState state,
        Action<TState> action) {
        try {
            action(state);
        } catch (Exception error) {
            failure = CombineFailures(failure, error);
        }
    }

    internal static void ThrowIfFailed(Exception? failure) {
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void DisposeMountResources() {
        Exception? failure = null;
        if (_mountResources is not null) {
            for (var index = _mountResources.Count - 1; index >= 0; index--) {
                CaptureException(
                    ref failure,
                    _mountResources[index].Dispose);
            }

            _mountResources.Clear();
        }

        CaptureException(
            ref failure,
            this,
            static node => node._mountCancellation?.Dispose());

        _mountCancellation = null;
        ThrowIfFailed(failure);
    }

    private static Exception CombineFailures(
        Exception? first,
        Exception second) {
        if (first is null) {
            return second;
        }

        return first is AggregateException aggregate
            ? new AggregateException(
                aggregate.InnerExceptions.Append(second))
            : new AggregateException(first, second);
    }
}
