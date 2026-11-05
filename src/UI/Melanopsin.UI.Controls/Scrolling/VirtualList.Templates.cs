namespace Melanopsin.UI.Controls;

public sealed partial class VirtualList {
    private bool _templateItemsDirty = true;
    private bool _usingTemplateChildren;
    private bool _replacingTemplateChildren;
    private int _firstRealized = -1;
    private int _lastRealized = -2;
    private Dictionary<Node, int> _realizedIndices =
        new(ReferenceEqualityComparer.Instance);

    private bool PrepareLayoutWindow(float viewportHeight) {
        _viewportHeight = Math.Max(0, viewportHeight);
        if (!UsesItemTemplate) {
            ClearTemplateChildren();
            return false;
        }

        EnsureRowOffsets();
        ClampScrollOffset(_viewportHeight);
        if (ItemCount == 0) {
            _firstRealized = -1;
            _lastRealized = -2;
            return ReplaceRealizedItems([], new Dictionary<Node, int>());
        }

        var first = _rowLayout.FindRowAt(_scrollOffset, ItemCount);
        first = Math.Max(0, first - Math.Max(0, Overscan));
        var lastVisible = _rowLayout.FindRowAt(
            _scrollOffset + Math.Max(1, _viewportHeight),
            ItemCount);
        var last = Math.Min(
            ItemCount - 1,
            lastVisible + Math.Max(0, Overscan));
        if (!_templateItemsDirty &&
            first == _firstRealized &&
            last == _lastRealized) {
            return false;
        }

        var previous = new Dictionary<object, Node>();
        foreach (var child in Children) {
            if (child.ReconciliationKey is { } key) {
                previous[key] = child;
            }
        }

        var next = new List<Node>(last - first + 1);
        var indices = new Dictionary<Node, int>(ReferenceEqualityComparer.Instance);
        var keys = new HashSet<object>();
        for (var index = first; index <= last; index++) {
            var key = ItemIdentity(index);
            if (!keys.Add(key)) {
                throw new InvalidOperationException(
                    $"Duplicate virtual item key '{key}'.");
            }

            if (!_templateItemsDirty &&
                previous.TryGetValue(key, out var retained) &&
                _realizedIndices.TryGetValue(retained, out var retainedIndex) &&
                retainedIndex == index) {
                next.Add(retained);
                indices[retained] = index;
                continue;
            }

            var content = ItemTemplate?.Invoke(index);
            if (content is null) {
                content = new TextBlock();
            } else if (content.Parent is not null) {
                throw new InvalidOperationException(
                    $"The template for virtual item {index} returned a node that already has a parent.");
            }

            var rowHeight = _rowLayout.GetRowHeight(index);
            var incoming = new VirtualListRow();
            incoming.ReconciliationKey = key;
            incoming.InlineStyle.Height = Length.Px(rowHeight);
            incoming.SetState(UiStateNames.Selected, index == _selectedIndex);
            incoming.AppendChild(content);
            var realized = previous.TryGetValue(key, out var current)
                ? TreeReconciler.Reconcile(current, incoming)
                : incoming;
            next.Add(realized);
            indices[realized] = index;
        }

        _firstRealized = first;
        _lastRealized = last;
        return ReplaceRealizedItems(next, indices);
    }

    internal int GetRealizedIndex(Node child) {
        return _realizedIndices.GetValueOrDefault(child, -1);
    }

    private void ClearTemplateChildren() {
        if (!_usingTemplateChildren) {
            return;
        }

        _usingTemplateChildren = false;
        _templateItemsDirty = true;
        _firstRealized = -1;
        _lastRealized = -2;
        _realizedIndices.Clear();
        ClearChildren();
    }

    private bool ReplaceRealizedItems(
        List<Node> next,
        Dictionary<Node, int> indices) {
        _templateItemsDirty = false;
        _usingTemplateChildren = true;
        if (ChildrenMatch(next)) {
            _realizedIndices = indices;
            return false;
        }

        _replacingTemplateChildren = true;
        try {
            ReplaceChildren(next);
        } finally {
            _replacingTemplateChildren = false;
        }

        _realizedIndices = indices;
        return true;
    }

    protected internal override void ReplaceChildren(
        IReadOnlyList<Node> children) {
        if (UsesItemTemplate && !_replacingTemplateChildren) {
            return;
        }

        base.ReplaceChildren(children);
    }

    private bool ChildrenMatch(List<Node> next) {
        if (Children.Count != next.Count) {
            return false;
        }

        for (var index = 0; index < Children.Count; index++) {
            if (!ReferenceEquals(Children[index], next[index])) {
                return false;
            }
        }

        return true;
    }

    private object ItemIdentity(int index) {
        var key = ItemKeyProvider?.Invoke(index);
        return key ?? new VirtualListIndexKey(index);
    }
}
