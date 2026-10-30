using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class FlexLayout :
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
    public Length ColumnGap { get; private set; } = Length.Space(1);

    [MarkupAttribute("row-gap", MarkupValueKind.Length)]
    public Length RowGap { get; private set; } = Length.Space(1);

    bool ILayoutViewportDependency.UsesViewportUnits =>
        ColumnGap.UsesViewportUnits || RowGap.UsesViewportUnits;

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "direction":
                Direction = ControlValueParser.ReadFlexDirection(value, Direction);
                break;
            case "wrap":
                Wrap = ControlValueParser.ReadFlexWrap(value, Wrap);
                break;
            case "justify-content":
                JustifyContent = ControlValueParser.ReadJustifyContent(
                    value,
                    JustifyContent);
                break;
            case "align-items":
                AlignItems = ControlValueParser.ReadLayoutAlignment(value, AlignItems);
                break;
            case "align-content":
                AlignContent = ControlValueParser.ReadLayoutAlignment(value, AlignContent);
                break;
            case "column-gap":
                if (Length.TryParse(value?.ToString(), out var columnGap)) {
                    ColumnGap = columnGap;
                }

                break;
            case "row-gap":
                if (Length.TryParse(value?.ToString(), out var rowGap)) {
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

    LayoutSize ILayoutParticipant.Measure(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        var style = context.GetStyle(this);
        var widthFallback = constraints.HasBoundedWidth
            ? constraints.MaxWidth
            : MeasureIntrinsicCore(
                context,
                LayoutAxis.Horizontal,
                LayoutIntrinsicMode.Maximum,
                constraints.HasBoundedHeight
                    ? constraints.MaxHeight
                    : float.PositiveInfinity);
        var contentWidth = context.ResolveWidth(style, constraints, widthFallback);
        var width = constraints.ConstrainWidth(contentWidth + (style.Margin * 2));
        var childConstraint = LayoutConstraints.Loose(
            Math.Max(
                0,
                width - (style.Padding * 2) - (style.Margin * 2)),
            constraints.HasBoundedHeight
                ? Math.Max(0, constraints.MaxHeight - (style.Padding * 2))
                : float.PositiveInfinity);
        var childrenHeight = MeasureChildren(context, childConstraint, style.Padding);
        var fallbackHeight = Math.Max(
            context.Metrics.Space(8),
            childrenHeight);
        var height = context.ResolveHeight(style, constraints, fallbackHeight);
        height = constraints.ConstrainHeight(height + (style.Margin * 2));
        return new LayoutSize(width, height);
    }

    void ILayoutParticipant.Arrange(
        LayoutArrangeContext context,
        Rect bounds) {
        var content = context.ContentBounds;
        var ordered = OrderChildren(context);
        if (ordered.Count == 0) {
            return;
        }

        var rowAxis = Direction is FlexDirection.Row or FlexDirection.RowReverse;
        var reverse = Direction is FlexDirection.RowReverse or FlexDirection.ColumnReverse;
        if (reverse) {
            ordered.Reverse();
        }

        var mainSize = rowAxis ? content.Width : content.Height;
        var crossSize = rowAxis ? content.Height : content.Width;
        var columnGap = ResolveGap(ColumnGap, context);
        var rowGap = ResolveGap(RowGap, context);
        var mainGap = rowAxis ? columnGap : rowGap;
        var crossGap = rowAxis ? rowGap : columnGap;
        EnsureArrangeCapacity(ordered.Count);
        for (var index = 0; index < ordered.Count; index++) {
            var crossConstraint = rowAxis
                ? LayoutConstraints.Loose(
                    content.Width,
                    float.PositiveInfinity)
                : LayoutConstraints.Loose(
                    float.PositiveInfinity,
                    content.Height);
            _measured[index] = context.Measure(
                ordered[index],
                crossConstraint);
            _styles[index] = context.GetStyle(ordered[index]);
            _items[index] = ResolveItem(ordered[index], _styles[index]);
        }

        if (Wrap == FlexWrap.NoWrap) {
            ArrangeLine(
                context,
                ordered,
                0,
                ordered.Count,
                rowAxis,
                reverse,
                content,
                mainSize,
                crossSize,
                mainGap);
            return;
        }

        _lines.Clear();
        var lineStart = 0;
        var usedMain = 0f;
        for (var index = 0; index < ordered.Count; index++) {
            var itemMain = rowAxis ? _measured[index].Width : _measured[index].Height;
            if (index > lineStart &&
                usedMain + mainGap + itemMain > mainSize) {
                _lines.Add(new LineRange(lineStart, index - lineStart));
                lineStart = index;
                usedMain = itemMain;
                continue;
            }

            usedMain += (index == lineStart ? 0 : mainGap) + itemMain;
        }

        if (lineStart < ordered.Count) {
            _lines.Add(new LineRange(lineStart, ordered.Count - lineStart));
        }

        EnsureLineCrossCapacity(_lines.Count);
        var totalCross = 0f;
        for (var lineIndex = 0; lineIndex < _lines.Count; lineIndex++) {
            var line = _lines[lineIndex];
            var lineCross = 0f;
            for (var index = line.Start; index < line.End; index++) {
                lineCross = Math.Max(
                    lineCross,
                    rowAxis ? _measured[index].Height : _measured[index].Width);
            }

            _lineCross[lineIndex] = lineCross;
            totalCross += lineCross;
        }

        totalCross += crossGap * Math.Max(0, _lines.Count - 1);
        var freeCross = Math.Max(0, crossSize - totalCross);
        if (AlignContent == LayoutAlignment.Stretch && _lines.Count > 0) {
            var growth = freeCross / _lines.Count;
            for (var index = 0; index < _lines.Count; index++) {
                _lineCross[index] += growth;
            }

            freeCross = 0;
        }

        var crossLeading = AlignContent switch {
            LayoutAlignment.Center => freeCross / 2,
            LayoutAlignment.End => freeCross,
            _ => 0,
        };
        var crossCursor = (rowAxis ? content.Y : content.X) + crossLeading;
        for (var offset = 0; offset < _lines.Count; offset++) {
            var lineIndex = Wrap == FlexWrap.WrapReverse
                ? _lines.Count - 1 - offset
                : offset;
            var line = _lines[lineIndex];
            var lineBounds = rowAxis
                ? new Rect(content.X, crossCursor, content.Width, _lineCross[lineIndex])
                : new Rect(crossCursor, content.Y, _lineCross[lineIndex], content.Height);
            ArrangeLine(
                context,
                ordered,
                line.Start,
                line.Count,
                rowAxis,
                reverse,
                lineBounds,
                mainSize,
                _lineCross[lineIndex],
                mainGap);
            crossCursor += _lineCross[lineIndex] + crossGap;
        }
    }

    float ILayoutIntrinsicProvider.MeasureIntrinsic(
        LayoutMeasureContext context,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent) {
        return MeasureIntrinsicCore(context, axis, mode, crossAxisExtent);
    }

    private float MeasureChildren(
        LayoutMeasureContext context,
        LayoutConstraints constraint,
        float padding) {
        var children = OrderChildren(context);
        if (children.Count == 0) {
            return 0;
        }

        var rowAxis = Direction is FlexDirection.Row or FlexDirection.RowReverse;
        var mainAvailable = rowAxis ? constraint.MaxWidth : constraint.MaxHeight;
        var columnGap = ResolveGap(ColumnGap, context);
        var rowGap = ResolveGap(RowGap, context);
        var gap = rowAxis ? columnGap : rowGap;
        var lineMain = 0f;
        var lineCross = 0f;
        var totalCross = 0f;
        var lineCount = 0;

        foreach (var child in children) {
            var measured = context.Measure(child, constraint);
            var childMain = rowAxis ? measured.Width : measured.Height;
            var childCross = rowAxis ? measured.Height : measured.Width;
            var required = lineMain == 0 ? childMain : lineMain + gap + childMain;
            if (Wrap != FlexWrap.NoWrap &&
                lineMain > 0 &&
                required > mainAvailable) {
                totalCross += lineCross +
                    (lineCount == 0 ? 0 : rowAxis ? rowGap : columnGap);
                lineCount++;
                lineMain = childMain;
                lineCross = childCross;
            } else {
                lineMain = required;
                lineCross = Math.Max(lineCross, childCross);
            }
        }

        totalCross += lineCross;
        return totalCross + (padding * 2);
    }

    private float MeasureIntrinsicCore(
        LayoutMeasureContext context,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent) {
        var style = context.GetStyle(this);
        var children = OrderChildren(context);
        if (children.Count == 0) {
            return style.Padding * 2;
        }

        var rowAxis = Direction is FlexDirection.Row or FlexDirection.RowReverse;
        var horizontal = axis == LayoutAxis.Horizontal;
        var mainAxis = rowAxis == horizontal;
        var gap = mainAxis
            ? ResolveGap(
                horizontal ? ColumnGap : RowGap,
                context)
            : 0;
        var maximum = 0f;
        var total = 0f;

        for (var index = 0; index < children.Count; index++) {
            var extent = context.MeasureIntrinsic(
                children[index],
                axis,
                mode,
                crossAxisExtent);
            maximum = Math.Max(maximum, extent);
            total += extent;
            if (index > 0) {
                total += gap;
            }
        }

        var mainExtent = mode == LayoutIntrinsicMode.Minimum && mainAxis
            ? maximum
            : total;
        var extentResult = mainAxis ? mainExtent : maximum;
        return extentResult + (style.Padding * 2);
    }

    private void ArrangeLine(
        LayoutArrangeContext context,
        IReadOnlyList<Node> children,
        int start,
        int count,
        bool rowAxis,
        bool reverse,
        Rect content,
        float mainSize,
        float crossSize,
        float gap) {
        var end = start + count;
        var totalMain = 0f;
        var totalGrow = 0f;
        var totalShrink = 0f;
        for (var index = start; index < end; index++) {
            _baseMain[index] = rowAxis
                ? _measured[index].Width
                : _measured[index].Height;
            totalMain += _baseMain[index];
            totalGrow += _items[index].Grow;
            totalShrink += _items[index].Shrink * _baseMain[index];
        }

        totalMain += gap * Math.Max(0, count - 1);
        var free = mainSize - totalMain;
        if (free < 0 && totalShrink > 0) {
            var overflow = -free;
            for (var index = start; index < end; index++) {
                var weight = _items[index].Shrink * _baseMain[index];
                _baseMain[index] = Math.Max(
                    0,
                    _baseMain[index] - (overflow * (weight / totalShrink)));
            }

            free = 0;
        } else if (free > 0 && totalGrow > 0) {
            for (var index = start; index < end; index++) {
                _baseMain[index] += free *
                    (_items[index].Grow / totalGrow);
            }

            free = 0;
        }

        free = Math.Max(0, free);
        var (leading, between) = ResolveMainDistribution(
            JustifyContent,
            free,
            count,
            gap);
        var cursor = rowAxis
            ? (reverse ? content.Right - leading : content.X + leading)
            : (reverse ? content.Bottom - leading : content.Y + leading);

        for (var index = start; index < end; index++) {
            var style = _styles[index];
            var itemMain = Math.Max(0, _baseMain[index] - (style.Margin * 2));
            var crossAvailable = Math.Max(0, crossSize - (style.Margin * 2));
            var naturalCross = Math.Max(
                0,
                (rowAxis ? _measured[index].Height : _measured[index].Width) -
                (style.Margin * 2));
            var alignment = _items[index].AlignSelf == LayoutAlignment.Auto
                ? AlignItems
                : _items[index].AlignSelf;
            var crossLength = rowAxis ? style.Height : style.Width;
            var itemCross = crossLength.Unit != LengthUnit.Auto
                ? naturalCross
                : alignment == LayoutAlignment.Stretch
                    ? crossAvailable
                    : Math.Min(naturalCross, crossAvailable);
            var crossOffset = alignment switch {
                LayoutAlignment.Center => (crossAvailable - itemCross) / 2,
                LayoutAlignment.End => crossAvailable - itemCross,
                _ => 0,
            };
            var main = reverse
                ? cursor - style.Margin - itemMain
                : cursor + style.Margin;
            var cross = (rowAxis ? content.Y : content.X) + style.Margin + crossOffset;
            var bounds = rowAxis
                ? new Rect(main, cross, itemMain, itemCross)
                : new Rect(cross, main, itemCross, itemMain);
            context.ArrangeChild(children[index], bounds);
            cursor += (reverse ? -1 : 1) *
                (_baseMain[index] + between);
        }
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

    private static float ResolveGap(
        Length gap,
        LayoutMeasureContext context) {
        return gap.Resolve(
            context.Metrics,
            available: 0,
            fallback: context.Metrics.Space(1));
    }

    private static float ResolveGap(
        Length gap,
        LayoutArrangeContext context) {
        return gap.Resolve(
            context.Metrics,
            available: 0,
            fallback: context.Metrics.Space(1));
    }

    private readonly record struct OrderedChild(
        Node Child,
        int Index,
        int Order);

    private readonly record struct LineRange(int Start, int Count) {
        public int End => Start + Count;
    }

    private static (float Leading, float Between) ResolveMainDistribution(
        JustifyContent justify,
        float free,
        int count,
        float gap) {
        if (count == 0) {
            return (0, gap);
        }

        return justify switch {
            JustifyContent.End => (free, gap),
            JustifyContent.Center => (free / 2, gap),
            JustifyContent.SpaceBetween when count > 1 =>
                (0, gap + (free / (count - 1))),
            JustifyContent.SpaceAround =>
                (free / (count * 2), gap + (free / count)),
            JustifyContent.SpaceEvenly =>
                (free / (count + 1), gap + (free / (count + 1))),
            _ => (0, gap),
        };
    }
}
