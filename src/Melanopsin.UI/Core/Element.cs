using Melanopsin.UI.Styling;

namespace Melanopsin.UI;

/// <summary>
/// An element in the compiled UI tree. Attribute names are finalized at build
/// time by the source generator; the runtime does not inspect XML files.
/// </summary>
public abstract partial class Element : Node {
    private static readonly IReadOnlyList<Node> NoChildren = Array.Empty<Node>();

    private readonly ElementState _state;
    private List<Node>? _children;
    private IReadOnlyList<Node>? _childrenView;

    protected Element(string type) {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        Type = type;
        _state = new ElementState(this);
    }

    public string Type { get; }

    public IReadOnlyDictionary<string, object?> Attributes => _state.Attributes;

    public IReadOnlyList<Node> Children => _childrenView ?? NoChildren;

    public InlineStyle InlineStyle => _state.InlineStyle;

    internal ElementState State => _state;

    internal bool HasInlineStyle => _state.HasInlineStyle;

    /// <summary>
    /// The backing child list, created on first use. Leaf elements never touch
    /// it, which keeps their footprint to the element itself.
    /// </summary>
    private List<Node> MutableChildren => _children ??= CreateChildList();

    private List<Node> CreateChildList() {
        var children = new List<Node>();
        _childrenView = children.AsReadOnly();
        return children;
    }

    /// <summary>
    /// Registers a value binding whose dependencies update this element directly.
    /// Binding reads are tracked independently from the owning component build.
    /// </summary>
    public void Bind<T>(Func<T> getter, Action<Element, T> apply) {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(apply);
        AddBinding(new ElementBinding<T>(this, getter, apply));
    }

    /// <summary>
    /// Binds a value to a named control attribute. The attribute name is part of
    /// the binding API so generated code does not depend on an implicit target.
    /// </summary>
    public void BindAttribute<T>(string name, Func<T> getter) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(getter);
        AddBinding(new ElementBinding<T>(this, name, getter));
    }

    /// <summary>Binds a value to typed attribute metadata declared by the control.</summary>
    public void BindAttribute<T>(ElementAttribute<T> attribute, Func<T> getter) {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(getter);
        AddBinding(new ElementBinding<T>(this, attribute, getter));
    }

    /// <summary>
    /// Registers an event subscription that follows this element when a rebuilt
    /// element is reconciled onto it and detaches when the element is removed.
    /// </summary>
    public void BindEvent(
        Action<Element> subscribe,
        Action<Element> unsubscribe) {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(unsubscribe);
        AddBinding(new ElementEventBinding(this, subscribe, unsubscribe));
    }

    private void AddBinding(ElementBinding binding) {
        _state.AddBinding(binding);
        if (IsMounted) {
            binding.Activate();
        }
    }

    public void SetAttributeValue(string name, object? value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attributeChanged = _state.TrySetAttribute(name, value);
        var changedInlineLayout = _state.TrySetInlineStyle(name, value);
        if (!attributeChanged && !changedInlineLayout) {
            return;
        }

        var attribute = FindAttribute(name);
        if (attributeChanged) {
            if (attribute is not null) {
                attribute.SetValue(this, value);
            } else {
                OnSetAttribute(name, value);
            }
        }

        if (!changedInlineLayout) {
            Invalidate(
                attribute?.Invalidation ?? ClassifyAttributeChange(name),
                LayoutInvalidationReason.LayoutProperty,
                affectsStyle: attribute?.AffectsStyle ?? true);
        }
    }

    /// <summary>Sets an attribute through its typed descriptor.</summary>
    public void SetAttributeValue<T>(ElementAttribute<T> attribute, T value) {
        ArgumentNullException.ThrowIfNull(attribute);
        var attributeChanged = _state.TrySetAttribute(attribute.Name, value);
        var changedInlineLayout = _state.TrySetInlineStyle(attribute.Name, value);
        if (!attributeChanged && !changedInlineLayout) {
            return;
        }

        if (attributeChanged) {
            attribute.SetValue(this, value);
        }

        if (!changedInlineLayout) {
            Invalidate(
                attribute.Invalidation,
                LayoutInvalidationReason.LayoutProperty,
                attribute.AffectsStyle);
        }
    }

    /// <summary>
    /// Resolves attribute metadata for dynamic string-based updates and element
    /// reconciliation. Generated markup normally bypasses this lookup.
    /// </summary>
    protected virtual ElementAttribute? FindAttribute(string name) => null;

    /// <summary>
    /// Applies a custom control attribute after it has been recorded on the element.
    /// Controls can override this method without requiring reflection at runtime.
    /// </summary>
    protected virtual void OnSetAttribute(string name, object? value) {
    }

    /// <summary>
    /// Classifies arbitrary attributes conservatively. Controls can override
    /// this when an attribute cannot affect measure or arrange.
    /// </summary>
    protected virtual InvalidationKind ClassifyAttributeChange(string name) {
        return name switch {
            "background" or "color" or "foreground" or
            "a11y-label" or "a11y-description" or "a11y-hidden" =>
                InvalidationKind.Paint,
            _ => InvalidationKind.LayoutAndPaint,
        };
    }

    internal InvalidationKind ApplyReconciledAttribute(string name, object? value) {
        var attribute = FindAttribute(name);
        if (attribute is not null) {
            attribute.SetValue(this, value);
        } else {
            OnSetAttribute(name, value);
        }

        return attribute?.Invalidation ?? ClassifyAttributeChange(name);
    }

    /// <summary>
    /// Called after a child has become a member of this element. Parent-owned
    /// layout metadata can be initialized or validated here.
    /// </summary>
    protected internal virtual void OnChildAdded(Node child) {
    }

    /// <summary>
    /// Called after a child has left this element. Parent-owned layout metadata
    /// associated with the child must be released here.
    /// </summary>
    protected internal virtual void OnChildRemoved(Node child) {
    }

    protected internal virtual InvalidationKind OnReconciledFrom(Element source) {
        return InvalidationKind.None;
    }

    /// <summary>
    /// Gets whether merging an incoming element of the same shape also replaces
    /// this element's children with the incoming children. Elements whose
    /// children come from somewhere other than their own markup return
    /// <see langword="false"/> to keep that subtree out of the parent's rebuild.
    /// </summary>
    protected internal virtual bool ReconcilesChildren => true;

    protected internal override void OnMounted() {
        base.OnMounted();
        SetOwnBindingsActive(active: true);
    }

    protected internal override void OnUnmounted() {
        SetOwnBindingsActive(active: false);
        base.OnUnmounted();
    }

    internal override void OnParentChanged(bool hasParent) {
        if (!hasParent) {
            SetBindingsActive(active: false);
        }
    }

    internal override void MountChildren(ref Exception? failure) {
        var children = _children;
        if (children is null) {
            return;
        }

        for (var index = 0; index < children.Count; index++) {
            Node.CaptureException(
                ref failure,
                children[index],
                static node => node.MountTree());
        }
    }

    internal override void UnmountChildren(ref Exception? failure) {
        var children = _children?.ToArray() ?? [];
        for (var index = children.Length - 1; index >= 0; index--) {
            Node.CaptureException(
                ref failure,
                children[index],
                static node => node.UnmountTree());
        }
    }

    private void SetBindingsActive(bool active) {
        SetOwnBindingsActive(active);
        var children = _children;
        if (children is null) {
            return;
        }

        foreach (var child in children) {
            if (child is Element element) {
                element.SetBindingsActive(active);
            }
        }
    }

    private void SetOwnBindingsActive(bool active) {
        _state.SetBindingsActive(active);
    }

    internal void OnInlineStyleChanged() {
        InvalidateLayout(LayoutInvalidationReason.LayoutProperty);
    }
}
