namespace Melanopsin.UI;

public abstract partial class Element {
    public virtual void AppendChild(Node child) {
        ArgumentNullException.ThrowIfNull(child);
        InsertChild(Children.Count, child);
    }

    public virtual void InsertChild(int index, Node child) {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, Children.Count);
        EnsureCanAdopt(child);

        var children = MutableChildren;
        children.Insert(index, child);
        child.SetParentState(this);
        CompleteChildAddition(child);
    }

    public virtual bool RemoveChild(Node child) {
        ArgumentNullException.ThrowIfNull(child);
        var children = _children;
        var index = children?.IndexOf(child) ?? -1;
        if (!ReferenceEquals(child.Parent, this) || index < 0) {
            return false;
        }

        children!.RemoveAt(index);
        child.SetParentState(null);
        CompleteChildRemoval(child);
        return true;
    }

    public virtual void ClearChildren() {
        var children = _children;
        if (children is null || children.Count == 0) {
            return;
        }

        var removed = children.ToArray();
        children.Clear();
        foreach (var child in removed) {
            child.SetParentState(null);
        }

        CompleteChildrenChange(removed, []);
    }

    public int IndexOfChild(Node child) {
        ArgumentNullException.ThrowIfNull(child);
        return _children?.IndexOf(child) ?? -1;
    }

    public bool ReplaceChild(Node current, Node replacement) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);
        var children = _children;
        var index = children?.IndexOf(current) ?? -1;
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
        children![index] = replacement;
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

        foreach (var child in Children) {
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

        foreach (var child in Children) {
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

        foreach (var child in Children) {
            if (child is not Element element) {
                continue;
            }

            foreach (var found in element.FindAll<T>()) {
                yield return found;
            }
        }
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
            Children,
            ReferenceEqualityComparer.Instance);
        var removed = Children
            .Where(child => !nextSet.Contains(child))
            .ToArray();
        var added = next
            .Where(child => !currentSet.Contains(child))
            .ToArray();
        var target = MutableChildren;
        target.Clear();
        target.AddRange(next);
        foreach (var child in removed) {
            child.SetParentState(null);
        }

        foreach (var child in added) {
            child.SetParentState(this);
        }

        CompleteChildrenChange(removed, added);
    }

    internal void DetachChildForTransfer(Node child) {
        if (!ReferenceEquals(child.Parent, this)) {
            throw new InvalidOperationException(
                "The node is not a child of this element.");
        }

        var children = _children;
        var index = children?.IndexOf(child) ?? -1;
        if (index < 0) {
            throw new InvalidOperationException(
                "The element's child list is inconsistent with the node parent.");
        }

        children!.RemoveAt(index);
        child.SetParentState(null);
        CompleteChildRemoval(child, invalidateLayout: false);
    }

    private bool ChildrenMatch(IReadOnlyList<Node> children) {
        var current = Children;
        if (current.Count != children.Count) {
            return false;
        }

        for (var index = 0; index < current.Count; index++) {
            if (!ReferenceEquals(current[index], children[index])) {
                return false;
            }
        }

        return true;
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
        Node.CaptureException(
            ref failure,
            (Owner: this, Child: child),
            static state => state.Owner.OnChildAdded(state.Child));
        Node.CaptureException(
            ref failure,
            child,
            static node => node.CompleteParentChange());
        Node.CaptureException(
            ref failure,
            this,
            static element => element.InvalidateLayout(LayoutInvalidationReason.Structure));
        Node.ThrowIfFailed(failure);
    }

    private void CompleteChildRemoval(
        Node child,
        bool invalidateLayout = true) {
        Exception? failure = null;
        Node.CaptureException(
            ref failure,
            (Owner: this, Child: child),
            static state => state.Owner.OnChildRemoved(state.Child));
        Node.CaptureException(
            ref failure,
            child,
            static node => node.CompleteParentChange());
        if (invalidateLayout) {
            Node.CaptureException(
                ref failure,
                this,
                static element => element.InvalidateLayout(LayoutInvalidationReason.Structure));
        }

        Node.ThrowIfFailed(failure);
    }

    private void CompleteChildrenChange(
        IReadOnlyList<Node> removed,
        IReadOnlyList<Node> added) {
        Exception? failure = null;
        foreach (var child in removed) {
            Node.CaptureException(
                ref failure,
                (Owner: this, Child: child),
                static state => state.Owner.OnChildRemoved(state.Child));
        }

        foreach (var child in added) {
            Node.CaptureException(
                ref failure,
                (Owner: this, Child: child),
                static state => state.Owner.OnChildAdded(state.Child));
        }

        foreach (var child in removed) {
            Node.CaptureException(
                ref failure,
                child,
                static node => node.CompleteParentChange());
        }

        foreach (var child in added) {
            Node.CaptureException(
                ref failure,
                child,
                static node => node.CompleteParentChange());
        }

        Node.CaptureException(
            ref failure,
            this,
            static element => element.InvalidateLayout(LayoutInvalidationReason.Structure));
        Node.ThrowIfFailed(failure);
    }
}
