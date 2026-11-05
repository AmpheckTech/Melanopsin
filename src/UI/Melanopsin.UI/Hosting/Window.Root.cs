// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.App;

namespace Melanopsin.UI.Hosting;

public sealed partial class Window {
    public Node Root {
        get => _root ?? throw new InvalidOperationException(
            $"Window '{Title}' has no root. Set Root before rendering.");
        set {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            var previous = _root;
            if (ReferenceEquals(previous, value)) {
                return;
            }

            if (value.Parent is not null) {
                throw new InvalidOperationException(
                    "A window root must be a detached node without a parent.");
            }

            if (value.IsMounted) {
                throw new InvalidOperationException(
                    "A window root cannot already be mounted by another owner.");
            }

            var runtime = _runtime;
            List<ComponentHost>? attachedHosts = null;
            try {
                if (runtime is not null) {
                    attachedHosts = AttachRuntimeToTree(value, runtime);
                }

                value.MountTree();
            } catch (Exception exception) {
                var failures = new List<Exception> { exception };
                try {
                    value.UnmountTree();
                } catch (Exception cleanupException) {
                    failures.Add(cleanupException);
                }

                DetachRuntimes(attachedHosts, runtime, failures);
                ThrowIfCleanupFailed(
                    "The new root failed to mount and could not be cleaned up.",
                    failures);
            }

            try {
                if (previous is not null) {
                    previous.Invalidated -= OnRootInvalidated;
                    previous.UnmountTree();
                }

                _root = value;
                value.Invalidated += OnRootInvalidated;
                InvalidateLayout(forceFullDamage: true);
            } catch (Exception exception) {
                var failures = new List<Exception> { exception };
                try {
                    value.Invalidated -= OnRootInvalidated;
                    value.UnmountTree();
                } catch (Exception cleanupException) {
                    failures.Add(cleanupException);
                }

                DetachRuntimes(attachedHosts, runtime, failures);

                if (previous is not null) {
                    try {
                        previous.Invalidated += OnRootInvalidated;
                        previous.MountTree();
                    } catch (Exception rollbackException) {
                        failures.Add(rollbackException);
                    }
                }

                _root = previous;
                if (failures.Count == 1) {
                    throw;
                }

                throw new AggregateException(
                    "The root replacement failed and could not be rolled back completely.",
                    failures);
            }
        }
    }

    internal void AttachRuntime(UiRuntime runtime) {
        if (_runtime is not null && !ReferenceEquals(_runtime, runtime)) {
            throw new InvalidOperationException(
                "A window cannot be attached to more than one UiRuntime.");
        }

        if (_root is not null) {
            AttachRuntimeToTree(_root, runtime);
        }

        _runtime = runtime;
    }
}
