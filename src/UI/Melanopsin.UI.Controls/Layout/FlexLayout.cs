using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed partial class FlexLayout :
    Control,
    ILayoutParticipant,
    ILayoutViewportDependency,
    ILayoutIntrinsicProvider {
    private readonly Dictionary<Node, FlexItem> _explicitItems =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<OrderedChild> _measureOrder = [];
    private readonly List<OrderedChild> _arrangeOrder = [];
    private readonly List<Node> _measureChildren = [];
    private readonly List<Node> _arrangeChildren = [];
    private readonly List<LineRange> _lines = [];
    private LayoutSize[] _measured = [];
    private ResolvedStyle[] _styles = [];
    private FlexItem[] _items = [];
    private float[] _baseMain = [];
    private float[] _lineCross = [];

    public FlexLayout()
        : base("FlexLayout") {
        IsRelayoutBoundary = true;
    }

    [MarkupAttribute("direction", MarkupValueKind.Enum)]
    public FlexDirection Direction { get; private set; } = FlexDirection.Row;

    [MarkupAttribute("wrap", MarkupValueKind.Enum)]
    public FlexWrap Wrap { get; private set; } = FlexWrap.NoWrap;

    [MarkupAttribute("justify-content", MarkupValueKind.Enum)]
    public JustifyContent JustifyContent { get; private set; } = JustifyContent.Start;

    [MarkupAttribute("align-items", MarkupValueKind.Enum)]
    public LayoutAlignment AlignItems { get; private set; } = LayoutAlignment.Stretch;

    [MarkupAttribute("align-content", MarkupValueKind.Enum)]
    public LayoutAlignment AlignContent { get; private set; } = LayoutAlignment.Start;

    [MarkupAttribute("column-gap", MarkupValueKind.Length)]
    public Length ColumnGap { get; private set; } = Length.Su(1);

    [MarkupAttribute("row-gap", MarkupValueKind.Length)]
    public Length RowGap { get; private set; } = Length.Su(1);

    bool ILayoutViewportDependency.UsesViewportUnits =>
        ColumnGap.UsesViewportUnits || RowGap.UsesViewportUnits;

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "direction":
                Direction = value is FlexDirection direction ? direction : Direction;
                break;
            case "wrap":
                Wrap = value is FlexWrap wrap ? wrap : Wrap;
                break;
            case "justify-content":
                JustifyContent = value is JustifyContent justifyContent
                    ? justifyContent
                    : JustifyContent;
                break;
            case "align-items":
                AlignItems = value is LayoutAlignment alignItems ? alignItems : AlignItems;
                break;
            case "align-content":
                AlignContent = value is LayoutAlignment alignContent ? alignContent : AlignContent;
                break;
            case "column-gap":
                if (value is Length columnGap) {
                    ColumnGap = columnGap;
                }

                break;
            case "row-gap":
                if (value is Length rowGap) {
                    RowGap = rowGap;
                }

                break;
        }
    }

    /// <summary>
    /// Sets flex item data owned by this layout. The value overrides the child's
    /// declarative flex properties and is released when the child leaves this layout.
    /// </summary>
    public void SetItem(Element child, FlexItem item) {
        ArgumentNullException.ThrowIfNull(child);
        if (!ReferenceEquals(child.Parent, this)) {
            throw new InvalidOperationException(
                "Flex item data can only be set for a direct child of this layout.");
        }

        if (!item.IsInitialized) {
            throw new ArgumentException(
                "Flex item data must be created with the FlexItem constructor.",
                nameof(item));
        }

        if (_explicitItems.TryGetValue(child, out var current) && current == item) {
            return;
        }

        _explicitItems[child] = item;
        InvalidateLayout();
    }

    /// <summary>
    /// Removes an explicit item override and restores the child's declarative
    /// flex-grow, flex-shrink, order and align-self values.
    /// </summary>
    public bool ClearItem(Element child) {
        ArgumentNullException.ThrowIfNull(child);
        if (!_explicitItems.Remove(child)) {
            return false;
        }

        InvalidateLayout();
        return true;
    }

    protected internal override void OnChildRemoved(Node child) {
        _explicitItems.Remove(child);
        base.OnChildRemoved(child);
    }

    private IReadOnlyList<Node> OrderChildren(LayoutMeasureContext context) {
        var source = context.GetVisibleChildren(this);
        _measureOrder.Clear();
        for (var index = 0; index < source.Count; index++) {
            var child = source[index];
            _measureOrder.Add(new OrderedChild(
                child,
                index,
                ResolveItem(child, context.GetStyle(child)).Order));
        }

        _measureOrder.Sort(static (left, right) => {
            var order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : left.Index.CompareTo(right.Index);
        });
        _measureChildren.Clear();
        foreach (var item in _measureOrder) {
            _measureChildren.Add(item.Child);
        }

        return _measureChildren;
    }

    private IReadOnlyList<Node> OrderChildren(LayoutArrangeContext context) {
        var source = context.GetVisibleChildren(this);
        _arrangeOrder.Clear();
        for (var index = 0; index < source.Count; index++) {
            var child = source[index];
            _arrangeOrder.Add(new OrderedChild(
                child,
                index,
                ResolveItem(child, context.GetStyle(child)).Order));
        }

        _arrangeOrder.Sort(static (left, right) => {
            var order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : left.Index.CompareTo(right.Index);
        });
        _arrangeChildren.Clear();
        foreach (var item in _arrangeOrder) {
            _arrangeChildren.Add(item.Child);
        }

        return _arrangeChildren;
    }

    private void EnsureArrangeCapacity(int count) {
        if (_measured.Length >= count) {
            return;
        }

        var capacity = Math.Max(count, Math.Max(4, _measured.Length * 2));
        _measured = new LayoutSize[capacity];
        _styles = new ResolvedStyle[capacity];
        _items = new FlexItem[capacity];
        _baseMain = new float[capacity];
    }

    private void EnsureLineCrossCapacity(int count) {
        if (_lineCross.Length >= count) {
            return;
        }

        _lineCross = new float[Math.Max(count, Math.Max(4, _lineCross.Length * 2))];
    }

    private FlexItem ResolveItem(Node child, ResolvedStyle style) {
        return _explicitItems.TryGetValue(child, out var item)
            ? item
            : new FlexItem(
                style.FlexGrow,
                style.FlexShrink,
                style.Order,
                style.AlignSelf);
    }

    private static float MainSize(LayoutSize size, bool rowAxis) =>
        rowAxis ? size.Width : size.Height;

    private static float CrossSize(LayoutSize size, bool rowAxis) =>
        rowAxis ? size.Height : size.Width;

    private readonly record struct OrderedChild(
        Node Child,
        int Index,
        int Order);

    private readonly record struct LineRange(int Start, int Count) {
        public int End => Start + Count;
    }
}
