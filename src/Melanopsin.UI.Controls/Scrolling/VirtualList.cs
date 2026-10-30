using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class VirtualList :
    Control,
    IInputHandler,
    ILayoutParticipant,
    IRenderCommandProvider,
    IScrollTransform {
    private int _selectedIndex = -1;
    private float _scrollOffset;
    private Func<int, string>? _itemTextProvider;
    private Func<int, float>? _rowHeightProvider;
    private Func<int, Node?>? _itemTemplate;
    private Func<int, object?>? _itemKeyProvider;
    private readonly VirtualListRowLayout _rowLayout = new();
    private bool _templateItemsDirty = true;
    private bool _usingTemplateChildren;
    private bool _replacingTemplateChildren;
    private int _firstRealized = -1;
    private int _lastRealized = -2;
    private Dictionary<Node, int> _realizedIndices =
        new(ReferenceEqualityComparer.Instance);
    // Layout-owned state consumed by input routing and paint-only rendering.
    private Rect _layoutBounds;
    private float _viewportHeight;

    public VirtualList()
        : base("VirtualList") {
        ClipToBounds = true;
        IsRepaintBoundary = true;
        IsRelayoutBoundary = true;
        InputHandler = this;
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.List;
    }

    [MarkupAttribute("item-count", MarkupValueKind.Integer)]
    public int ItemCount { get; private set; }

    [MarkupAttribute("row-height", MarkupValueKind.Number)]
    public float RowHeight { get; private set; } = 32;

    [MarkupAttribute("overscan", MarkupValueKind.Integer)]
    public int Overscan { get; private set; } = 1;

    public int SelectedIndex => _selectedIndex;

    public float ScrollOffset => _scrollOffset;

    public float OffsetX => 0;

    public float OffsetY => _scrollOffset;

    public bool UsesItemTemplate => _itemTemplate is not null;

    public int FirstRealizedIndex => _firstRealized;

    public int LastRealizedIndex => _lastRealized;

    public int RealizedItemCount => UsesItemTemplate ? Children.Count : 0;

    public Func<int, string>? ItemTextProvider {
        get => _itemTextProvider;
        set {
            if (ReferenceEquals(_itemTextProvider, value)) {
                return;
            }

            _itemTextProvider = value;
            InvalidatePaint();
        }
    }

    public Func<int, float>? RowHeightProvider {
        get => _rowHeightProvider;
        set {
            if (ReferenceEquals(_rowHeightProvider, value)) {
                return;
            }

            _rowHeightProvider = value;
            InvalidateAllRowHeights();
        }
    }

    /// <summary>
    /// Produces the node tree for an item. Only visible rows and overscan are
    /// materialized; returning null is treated as an empty row.
    /// </summary>
    public Func<int, Node?>? ItemTemplate {
        get => _itemTemplate;
        set {
            if (ReferenceEquals(_itemTemplate, value)) {
                return;
            }

            _itemTemplate = value;
            RefreshItems();
        }
    }

    /// <summary>
    /// Supplies a stable identity for each item. Null keys fall back to the
    /// logical index and therefore do not preserve identity across insertions.
    /// </summary>
    public Func<int, object?>? ItemKeyProvider {
        get => _itemKeyProvider;
        set {
            if (ReferenceEquals(_itemKeyProvider, value)) {
                return;
            }

            _itemKeyProvider = value;
            RefreshItems();
        }
    }

    public float TotalContentHeight {
        get {
            EnsureRowOffsets();
            return _rowLayout.TotalHeight;
        }
    }

    public event Action<int>? SelectedIndexChanged;

    public void SetItemCount(int count) {
        var next = Math.Max(0, count);
        if (ItemCount == next) {
            return;
        }

        ItemCount = next;
        _selectedIndex = Math.Clamp(_selectedIndex, -1, ItemCount - 1);
        EnsureRowOffsets();
        ClampScrollOffset(_viewportHeight);
        InvalidateItemGeometry();
        if (UsesItemTemplate) {
            RefreshItems();
        }
    }

    /// <summary>
    /// Rebases cached offsets after the height provider changes one row.
    /// </summary>
    public void InvalidateRowHeight(int index) {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, ItemCount);
        _rowLayout.InvalidateRow(index);
        ClampScrollOffset(_viewportHeight);
        InvalidateItemGeometry();
        if (UsesItemTemplate) {
            RefreshItems();
        }
    }

    public void Select(int index) {
        var next = index >= 0 && index < ItemCount ? index : -1;
        if (_selectedIndex == next) {
            return;
        }

        _selectedIndex = next;
        UpdateRealizedSelection();
        Accessibility.Value = next.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        InvalidatePaint();
        SelectedIndexChanged?.Invoke(next);
    }

    public void ScrollTo(int index) {
        if (index < 0 || index >= ItemCount) {
            return;
        }

        EnsureRowOffsets();
        var rowTop = _rowLayout.GetRowTop(index);
        var rowBottom = rowTop + _rowLayout.GetRowHeight(index);
        if (rowTop < _scrollOffset) {
            _scrollOffset = rowTop;
        } else if (rowBottom > _scrollOffset + _viewportHeight) {
            _scrollOffset = rowBottom - _viewportHeight;
        }

        ClampScrollOffset(_viewportHeight);
        UpdateRealizedItems();
    }

    public void ScrollBy(float deltaY) {
        EnsureRowOffsets();
        var maximum = _rowLayout.MaximumScrollOffset(_viewportHeight);
        var next = Math.Clamp(_scrollOffset + deltaY, 0, maximum);
        if (Math.Abs(next - _scrollOffset) < 0.001f) {
            return;
        }

        _scrollOffset = next;
        UpdateRealizedItems();
    }

    /// <summary>
    /// Rebuilds and reconciles the realized item templates. Call this when item
    /// data changes without changing <see cref="ItemCount"/>.
    /// </summary>
    public void RefreshItems() {
        _templateItemsDirty = true;
        InvalidateLayout();
    }

    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            return;
        }

        var input = inputEvent.Input;
        if (input.Phase == PointerPhase.Scroll && input.WheelDelta != 0) {
            ScrollBy(-input.WheelDelta / 120f * 48);
            inputEvent.Handled = true;
            return;
        }

        if (input.Phase != PointerPhase.Down ||
            _viewportHeight <= 0) {
            return;
        }

        EnsureRowOffsets();
        var localY = input.Y - _layoutBounds.Y + _scrollOffset;
        var row = _rowLayout.FindRowAt(localY, ItemCount);
        if (row >= 0 && row < ItemCount) {
            Select(row);
            inputEvent.Handled = true;
        }
    }

    /// <summary>
    /// Emits paint commands from the latest layout snapshot. It does not
    /// correct scroll bounds or materialize rows.
    /// </summary>
    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (ItemCount == 0 || context.Bounds.Height <= 0) {
            return;
        }

        if (UsesItemTemplate) {
            AppendSelectionCommands(context, commands);
            return;
        }

        var first = _rowLayout.FindRowAt(_scrollOffset, ItemCount);
        first = Math.Max(0, first - Math.Max(0, Overscan));
        var lastVisible = _rowLayout.FindRowAt(
            _scrollOffset + context.Bounds.Height,
            ItemCount);
        var last = Math.Min(
            ItemCount - 1,
            lastVisible + Math.Max(0, Overscan));
        for (var index = first; index <= last; index++) {
            var rowHeight = _rowLayout.GetRowHeight(index);
            var rowBounds = new Rect(
                context.Bounds.X,
                context.Bounds.Y + _rowLayout.GetRowTop(index) - _scrollOffset,
                context.Bounds.Width,
                rowHeight);
            if (index == _selectedIndex) {
                commands.Add(new FillRectangleCommand(
                    rowBounds,
                    new Color(224, 238, 232),
                    context.CornerRadius * 0.5f));
            }

            context.AppendText(
                commands,
                ItemTextProvider?.Invoke(index) ?? $"Item {index + 1}",
                new Rect(
                    rowBounds.X + context.Padding,
                    rowBounds.Y,
                    Math.Max(0, rowBounds.Width - (context.Padding * 2)),
                    rowBounds.Height),
                context.Foreground,
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center,
                wrap: false);
        }
    }

    LayoutSize ILayoutParticipant.Measure(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        var style = context.GetStyle(this);
        var widthFallback = constraints.HasBoundedWidth
            ? constraints.MaxWidth
            : 0;
        var contentWidth = context.ResolveWidth(
            style,
            constraints,
            widthFallback);
        var width = constraints.ConstrainWidth(
            contentWidth + (style.Margin * 2));
        var contentConstraint = LayoutConstraints.Loose(
            Math.Max(
                0,
                width - (style.Padding * 2) - (style.Margin * 2)),
            constraints.HasBoundedHeight
                ? Math.Max(0, constraints.MaxHeight - (style.Padding * 2))
                : float.PositiveInfinity);
        var viewportHeight = context.ResolveHeight(
            style,
            contentConstraint,
            Math.Max(
                context.Metrics.Space(12),
                RowHeight * 4));
        PrepareLayoutWindow(Math.Min(
            contentConstraint.MaxHeight,
            Math.Max(0, viewportHeight)));
        var children = context.GetVisibleChildren(this);
        var childrenHeight = 0f;
        if (UsesItemTemplate) {
            foreach (var child in children) {
                var measured = context.Measure(
                    child,
                    LayoutConstraints.Loose(
                        contentConstraint.MaxWidth,
                        contentConstraint.MaxHeight));
                childrenHeight = Math.Max(childrenHeight, measured.Height);
            }
        }

        var fallbackHeight = Math.Max(
            Math.Max(context.Metrics.Space(12), RowHeight * 4),
            childrenHeight);
        var height = context.ResolveHeight(
            style,
            constraints,
            fallbackHeight);
        height = constraints.ConstrainHeight(height + (style.Margin * 2));
        return new LayoutSize(width, height);
    }

    void ILayoutParticipant.Arrange(
        LayoutArrangeContext context,
        Rect bounds) {
        _layoutBounds = bounds;
        var content = context.ContentBounds;
        PrepareLayoutWindow(content.Height);
        if (!UsesItemTemplate) {
            return;
        }

        foreach (var child in context.GetVisibleChildren(this)) {
            var index = GetRealizedIndex(child);
            if (index < 0) {
                continue;
            }

            var height = _rowLayout.GetRowHeight(index);
            _ = context.Measure(
                child,
                LayoutConstraints.Loose(content.Width, height));
            context.ArrangeChild(
                child,
                new Rect(
                    content.X,
                    content.Y + _rowLayout.GetRowTop(index),
                    content.Width,
                    height));
        }
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "item-count":
                SetItemCount(ControlValueParser.ReadInteger(value) ?? ItemCount);
                break;
            case "row-height":
                RowHeight = Math.Max(
                    1,
                    ControlValueParser.ReadNumber(value) ?? RowHeight);
                InvalidateAllRowHeights();
                break;
            case "overscan":
                Overscan = Math.Max(
                    0,
                    ControlValueParser.ReadInteger(value) ?? Overscan);
                InvalidateItemGeometry();
                break;
        }
    }

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
            incoming.InlineStyle.Height = Length.Pixels(rowHeight);
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

    private void ClampScrollOffset(float? viewportHeight) {
        EnsureRowOffsets();
        var viewport = viewportHeight ?? _viewportHeight;
        var maximum = _rowLayout.MaximumScrollOffset(viewport);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maximum);
    }

    private void InvalidateAllRowHeights() {
        _rowLayout.Invalidate();
        ClampScrollOffset(_viewportHeight);
        InvalidateItemGeometry();
        if (UsesItemTemplate) {
            RefreshItems();
        }
    }

    private void InvalidateItemGeometry() {
        if (UsesItemTemplate) {
            InvalidateLayout();
        } else {
            InvalidatePaint();
        }
    }

    private void UpdateRealizedItems() {
        if (UsesItemTemplate && PrepareLayoutWindow(_viewportHeight)) {
            return;
        }

        InvalidatePaint();
    }

    private void UpdateRealizedSelection() {
        foreach (var child in Children) {
            child.SetState(
                UiStateNames.Selected,
                GetRealizedIndex(child) == _selectedIndex);
        }
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

    private void AppendSelectionCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (_selectedIndex < 0) {
            return;
        }

        foreach (var child in Children) {
            if (GetRealizedIndex(child) != _selectedIndex) {
                continue;
            }

            var rowBounds = new Rect(
                context.Bounds.X + context.Padding,
                context.Bounds.Y + context.Padding +
                    _rowLayout.GetRowTop(_selectedIndex) - _scrollOffset,
                Math.Max(0, context.Bounds.Width - (context.Padding * 2)),
                _rowLayout.GetRowHeight(_selectedIndex));
            commands.Add(new FillRectangleCommand(
                rowBounds,
                new Color(224, 238, 232),
                context.CornerRadius * 0.5f));
        }
    }

    private void EnsureRowOffsets() {
        _rowLayout.Ensure(ItemCount, RowHeight, _rowHeightProvider);
    }
}
