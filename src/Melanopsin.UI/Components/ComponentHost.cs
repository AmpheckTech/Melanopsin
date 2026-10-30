using Melanopsin.UI.App;

namespace Melanopsin.UI;

/// <summary>
/// Connects a component's generated UI tree to the element tree.
/// </summary>
public sealed class ComponentHost : Control {
    private readonly Component _component;
    private Node? _content;
    private bool _isBuildDirty;

    public ComponentHost(Component component, UiRuntime? runtime = null)
        : base("ComponentHost") {
        ArgumentNullException.ThrowIfNull(component);
        _component = component;
        _component.AttachHost(this);
        if (runtime is not null) {
            AttachRuntime(runtime);
        }

        IsRelayoutBoundary = true;
        SetAttributeValue("input", "none");
        _component.Invalidated += OnComponentInvalidated;
        Rebuild();
    }

    public Component Component => _component;

    public UiRuntime? Runtime { get; private set; }

    /// <summary>
    /// Gets the reconciled content, flushing a pending frame build if needed.
    /// </summary>
    public Node? Content {
        get {
            FlushPendingBuild();
            return _content;
        }
    }

    public bool IsBuildDirty => _isBuildDirty;

    public void Rebuild() {
        _isBuildDirty = false;
        var incoming = _component.BuildForHost();
        if (_content is null) {
            _content = incoming;
            AppendChild(incoming);
            _component.CompleteBuild(_content);
            return;
        }

        var reconciled = TreeReconciler.Reconcile(_content, incoming);
        if (!ReferenceEquals(reconciled, _content)) {
            RemoveChild(_content);
            _content = reconciled;
            AppendChild(reconciled);
        }

        _component.CompleteBuild(_content);
    }

    internal void AttachRuntime(UiRuntime runtime) {
        ArgumentNullException.ThrowIfNull(runtime);
        if (Runtime is not null && !ReferenceEquals(Runtime, runtime)) {
            throw new InvalidOperationException(
                "A component host cannot be attached to more than one UiRuntime.");
        }

        Runtime = runtime;
    }

    internal void DetachRuntime(UiRuntime runtime) {
        ArgumentNullException.ThrowIfNull(runtime);
        if (ReferenceEquals(Runtime, runtime)) {
            Runtime = null;
        }
    }

    public void Disconnect() {
        _component.Invalidated -= OnComponentInvalidated;
        _component.DetachHost(this);
        _isBuildDirty = false;
        if (_content is not null) {
            RemoveChild(_content);
            _content = null;
        }
    }

    private void OnComponentInvalidated(Component component) {
        _isBuildDirty = true;
        InvalidateBuild();
    }

    internal void FlushPendingBuild() {
        if (!_isBuildDirty) {
            return;
        }

        Rebuild();
    }
}
