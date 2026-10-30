using Melanopsin.UI.Styling;

namespace Melanopsin.UI;

/// <summary>
/// An element in the compiled UI tree. Attribute names are finalized at build
/// time by the source generator; the runtime does not inspect XML files.
/// </summary>
public abstract class Element : Node {
    private readonly List<Node> _children = [];
    private readonly IReadOnlyList<Node> _childrenView;
    private readonly ElementState _state = new();

    protected Element(string type) {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        Type = type;
        _childrenView = _children.AsReadOnly();
        InlineStyle.Changed += OnInlineStyleChanged;
    }

    public string Type { get; }

    public IReadOnlyDictionary<string, object?> Attributes => _state.Attributes;

    public IReadOnlyList<Node> Children => _childrenView;

    public InlineStyle InlineStyle => _state.InlineStyle;

    internal ElementState State => _state;

    /// <summary>
    /// Registers a value binding whose dependencies update this element directly.
    /// Binding reads are tracked independently from the owning component build.
    /// </summary>
    public void Bind<T>(Func<T> getter, Action<Element, T> apply) {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(apply);
        var binding = new ElementBinding<T>(this, getter, apply);
        _state.AddBinding(binding);
        if (IsMounted || Parent is not null) {
            binding.Activate();
        }
    }

    public void SetAttributeValue(string name, object? value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attributeChanged = _state.TrySetAttribute(name, value);
        var changedInlineLayout = InlineStyle.TrySet(name, value);
        if (!attributeChanged && !changedInlineLayout) {
            return;
        }

        if (attributeChanged) {
            OnSetAttribute(name, value);
        }

        if (!changedInlineLayout) {
            Invalidate(
                ClassifyAttributeChange(name),
                LayoutInvalidationReason.LayoutProperty,
                affectsStyle: true);
        }
    }

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
        OnSetAttribute(name, value);
        return ClassifyAttributeChange(name);
    }

    public virtual void AppendChild(Node child) {
        InsertChild(_children.Count, child);
    }

    public virtual void InsertChild(int index, Node child) {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _children.Count);
        EnsureCanAdopt(child);

        _children.Insert(index, child);
        child.SetParentState(this);
        CompleteChildAddition(child);
    }

    public virtual bool RemoveChild(Node child) {
        ArgumentNullException.ThrowIfNull(child);
        var index = _children.IndexOf(child);
        if (!ReferenceEquals(child.Parent, this) || index < 0) {
            return false;
        }

        _children.RemoveAt(index);
        child.SetParentState(null);
        CompleteChildRemoval(child);
        return true;
    }

    public virtual void ClearChildren() {
        if (_children.Count == 0) {
            return;
        }

        var removed = _children.ToArray();
        _children.Clear();
        foreach (var child in removed) {
            child.SetParentState(null);
        }

        CompleteChildrenChange(removed, []);
    }

    public int IndexOfChild(Node child) {
        ArgumentNullException.ThrowIfNull(child);
        return _children.IndexOf(child);
    }

    public bool ReplaceChild(Node current, Node replacement) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);
        var index = _children.IndexOf(current);
        if (index < 0) {
            return false;
        }

        if (ReferenceEquals(current, replacement)) {
            return true;
        }

        if (replacement.Parent is not null) {
            throw new InvalidOperationException(
                "The replacement UI node already belongs to a parent.");
        }

        EnsureNoCycle(replacement);
        _children[index] = replacement;
        current.SetParentState(null);
        replacement.SetParentState(this);
        CompleteChildrenChange([current], [replacement]);
        return true;
    }

    public Node? FindById(string id) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (this is Control { Id: { } currentId } &&
            string.Equals(currentId, id, StringComparison.Ordinal)) {
            return this;
        }

        foreach (var child in _children) {
            if (child is Element element && element.FindById(id) is { } match) {
                return match;
            }
        }

        return null;
    }

    public T? FindFirst<T>()
        where T : Node {
        if (this is T match) {
            return match;
        }

        foreach (var child in _children) {
            if (child is Element element && element.FindFirst<T>() is { } found) {
                return found;
            }
        }

        return null;
    }

    public IEnumerable<T> FindAll<T>()
        where T : Node {
        if (this is T match) {
            yield return match;
        }

        foreach (var child in _children) {
            if (child is not Element element) {
                continue;
            }

            foreach (var found in element.FindAll<T>()) {
                yield return found;
            }
        }
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

    protected internal virtual void ReplaceChildren(
        IReadOnlyList<Node> children) {
        ArgumentNullException.ThrowIfNull(children);
        var next = children.ToArray();
        if (ChildrenMatch(next)) {
            return;
        }

        var nextSet = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        foreach (var child in next) {
            ArgumentNullException.ThrowIfNull(child, nameof(children));
            if (!nextSet.Add(child)) {
                throw new ArgumentException(
                    "A child can only appear once in an element's child list.",
                    nameof(children));
            }

            if (child.Parent is not null && !ReferenceEquals(child.Parent, this)) {
                throw new InvalidOperationException(
                    "A UI node cannot belong to more than one parent.");
            }

            EnsureNoCycle(child);
        }

        var currentSet = new HashSet<Node>(
            _children,
            ReferenceEqualityComparer.Instance);
        var removed = _children
            .Where(child => !nextSet.Contains(child))
            .ToArray();
        var added = next
            .Where(child => !currentSet.Contains(child))
            .ToArray();
        _children.Clear();
        _children.AddRange(next);
        foreach (var child in removed) {
            child.SetParentState(null);
        }

        foreach (var child in added) {
            child.SetParentState(this);
        }

        CompleteChildrenChange(removed, added);
    }

    internal void DetachChildForTransfer(Node child) {
        ArgumentNullException.ThrowIfNull(child);
        if (!ReferenceEquals(child.Parent, this)) {
            throw new InvalidOperationException(
                "The node is not a child of this element.");
        }

        var index = _children.IndexOf(child);
        if (index < 0) {
            throw new InvalidOperationException(
                "The element's child list is inconsistent with the node parent.");
        }

        _children.RemoveAt(index);
        child.SetParentState(null);
        CompleteChildRemoval(child, invalidateLayout: false);
    }

    private bool ChildrenMatch(IReadOnlyList<Node> children) {
        if (_children.Count != children.Count) {
            return false;
        }

        for (var index = 0; index < _children.Count; index++) {
            if (!ReferenceEquals(_children[index], children[index])) {
                return false;
            }
        }

        return true;
    }

    public void AppendContent(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return;
        }

        if (this is ITextContentControl contentControl) {
            contentControl.AppendText(value);
        } else {
            AppendChild(new TextNode(value));
        }
    }

    public void SetTextContent(string? value) {
        value ??= string.Empty;
        if (this is ITextContentControl contentControl) {
            contentControl.Text = value;
            return;
        }

        var index = _children.FindIndex(static child => child is TextNode);
        if (index < 0) {
            if (value.Length != 0) {
                InsertChild(0, new TextNode(value));
            }

            return;
        }

        var current = (TextNode)_children[index];
        if (string.Equals(current.Text, value, StringComparison.Ordinal)) {
            return;
        }

        if (value.Length == 0) {
            RemoveChild(current);
            return;
        }

        ReplaceChild(current, new TextNode(value));
    }

    private void EnsureCanAdopt(Node child) {
        if (child.Parent is not null) {
            throw new InvalidOperationException(
                "A UI node cannot belong to more than one parent.");
        }

        EnsureNoCycle(child);
    }

    private void EnsureNoCycle(Node child) {
        for (Node? ancestor = this; ancestor is not null; ancestor = ancestor.Parent) {
            if (ReferenceEquals(ancestor, child)) {
                throw new InvalidOperationException(
                    "Attaching the node would create a cycle in the UI tree.");
            }
        }
    }

    private void CompleteChildAddition(Node child) {
        Exception? failure = null;
        Node.CaptureException(ref failure, () => OnChildAdded(child));
        Node.CaptureException(ref failure, child.CompleteParentChange);
        Node.CaptureException(
            ref failure,
            () => InvalidateLayout(LayoutInvalidationReason.Structure));
        Node.ThrowIfFailed(failure);
    }

    private void CompleteChildRemoval(
        Node child,
        bool invalidateLayout = true) {
        Exception? failure = null;
        Node.CaptureException(ref failure, () => OnChildRemoved(child));
        Node.CaptureException(ref failure, child.CompleteParentChange);
        if (invalidateLayout) {
            Node.CaptureException(
                ref failure,
                () => InvalidateLayout(LayoutInvalidationReason.Structure));
        }

        Node.ThrowIfFailed(failure);
    }

    private void CompleteChildrenChange(
        IReadOnlyList<Node> removed,
        IReadOnlyList<Node> added) {
        Exception? failure = null;
        foreach (var child in removed) {
            Node.CaptureException(ref failure, () => OnChildRemoved(child));
        }

        foreach (var child in added) {
            Node.CaptureException(ref failure, () => OnChildAdded(child));
        }

        foreach (var child in removed) {
            Node.CaptureException(ref failure, child.CompleteParentChange);
        }

        foreach (var child in added) {
            Node.CaptureException(ref failure, child.CompleteParentChange);
        }

        Node.CaptureException(
            ref failure,
            () => InvalidateLayout(LayoutInvalidationReason.Structure));
        Node.ThrowIfFailed(failure);
    }

    protected internal override void OnMounted() {
        base.OnMounted();
        SetOwnBindingsActive(active: true);
    }

    protected internal override void OnUnmounted() {
        SetOwnBindingsActive(active: false);
        base.OnUnmounted();
    }

    internal override void OnParentChanged(bool hasParent) {
        SetBindingsActive(hasParent);
    }

    internal override void MountChildren(ref Exception? failure) {
        for (var index = 0; index < _children.Count; index++) {
            Node.CaptureException(ref failure, _children[index].MountTree);
        }
    }

    internal override void UnmountChildren(ref Exception? failure) {
        var children = _children.ToArray();
        for (var index = children.Length - 1; index >= 0; index--) {
            Node.CaptureException(ref failure, children[index].UnmountTree);
        }
    }

    private void SetBindingsActive(bool active) {
        SetOwnBindingsActive(active);
        foreach (var child in _children) {
            if (child is Element element) {
                element.SetBindingsActive(active);
            }
        }
    }

    private void SetOwnBindingsActive(bool active) {
        _state.SetBindingsActive(active);
    }

    private void OnInlineStyleChanged() {
        InvalidateLayout(LayoutInvalidationReason.LayoutProperty);
    }
}
