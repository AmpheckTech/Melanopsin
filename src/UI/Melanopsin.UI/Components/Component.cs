// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Reactive;
using Melanopsin.UI.App;

namespace Melanopsin.UI;

/// <summary>
/// A reusable application component that owns state and produces a UI tree.
/// </summary>
public abstract class Component : IContextStore, IReactiveInvalidationTarget {
    private long _revision;
    private HashSet<IReactiveSource> _reactiveSources = [];

    Dictionary<object, object>? IContextStore.ContextValues { get; set; }

    public event Action<Component>? Invalidated;

    public long Revision => Interlocked.Read(ref _revision);

    internal ComponentHost? Host { get; private set; }

    /// <summary>
    /// Gets the UI runtime supplied by the owning component host.
    /// Standalone component builds do not invent an ambient runtime.
    /// </summary>
    public UiRuntime? Runtime => Host?.Runtime;

    public Node Build() {
        var root = BuildForHost();
        OnBuildCompleted(root);
        return root;
    }

    internal Node BuildForHost() {
        var dependencies = new HashSet<IReactiveSource>();
        var tentativeSubscriptions = new HashSet<IReactiveSource>();
        Node root;
        using var componentScope = ComponentBuildScope.Enter(this);
        try {
            using (ReactiveRuntime.Track(
                dependencies,
                source => SubscribeDuringBuild(source, tentativeSubscriptions))) {
                root = BuildCore();
            }

            ArgumentNullException.ThrowIfNull(root);
            if (root.Parent is not null) {
                throw new InvalidOperationException(
                    "A component build result cannot already belong to another parent.");
            }

            UpdateReactiveSources(dependencies, tentativeSubscriptions);
            return root;
        } catch {
            Unsubscribe(tentativeSubscriptions);
            throw;
        }
    }

    internal void CompleteBuild(Node root) {
        OnBuildCompleted(root);
    }

    internal void AttachHost(ComponentHost host) {
        Host = host;
    }

    internal void DetachHost(ComponentHost host) {
        if (ReferenceEquals(Host, host)) {
            Host = null;
        }
    }

    protected abstract Node BuildCore();

    /// <summary>
    /// Gets the resource resolver for generated resource bindings. Calling this
    /// from a component without an attached host fails explicitly instead of
    /// falling back to a process-wide resource store.
    /// </summary>
    protected ResourceResolver GetResources() {
        return Runtime?.Resources ??
            throw new InvalidOperationException(
                "Resource bindings require the component to be attached to a UiRuntime.");
    }

    protected virtual void OnBuildCompleted(Node root) {
    }

    protected void Invalidate() {
        Interlocked.Increment(ref _revision);
        Invalidated?.Invoke(this);
    }

    void IReactiveInvalidationTarget.InvalidateFromReactiveSource() {
        Invalidate();
    }

    private void SubscribeDuringBuild(
        IReactiveSource source,
        HashSet<IReactiveSource> tentativeSubscriptions) {
        if (_reactiveSources.Contains(source) || !tentativeSubscriptions.Add(source)) {
            return;
        }

        source.Invalidated += OnReactiveSourceInvalidated;
    }

    private void UpdateReactiveSources(
        HashSet<IReactiveSource> dependencies,
        HashSet<IReactiveSource> tentativeSubscriptions) {
        foreach (var source in _reactiveSources) {
            if (!dependencies.Contains(source)) {
                source.Invalidated -= OnReactiveSourceInvalidated;
            }
        }

        foreach (var source in dependencies) {
            if (!_reactiveSources.Contains(source) && !tentativeSubscriptions.Contains(source)) {
                source.Invalidated += OnReactiveSourceInvalidated;
            }
        }

        _reactiveSources = dependencies;
    }

    private void Unsubscribe(HashSet<IReactiveSource> subscriptions) {
        foreach (var source in subscriptions) {
            source.Invalidated -= OnReactiveSourceInvalidated;
        }
    }

    private void OnReactiveSourceInvalidated(IReactiveSource source) {
        ReactiveRuntime.RequestInvalidation(this);
    }
}
