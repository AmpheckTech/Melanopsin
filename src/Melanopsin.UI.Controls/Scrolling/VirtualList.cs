using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed partial class VirtualList :
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

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "item-count":
                SetItemCount(value is int itemCount ? itemCount : ItemCount);
                break;
            case "row-height":
                RowHeight = Math.Max(
                    1,
                    value is float rowHeight ? rowHeight : RowHeight);
                InvalidateAllRowHeights();
                break;
            case "overscan":
                Overscan = Math.Max(
                    0,
                    value is int overscan ? overscan : Overscan);
                InvalidateItemGeometry();
                break;
        }
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

    private void EnsureRowOffsets() {
        _rowLayout.Ensure(ItemCount, RowHeight, _rowHeightProvider);
    }
}
