using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class GridLayout :
    Control,
    ILayoutParticipant,
    ILayoutViewportDependency,
    ILayoutIntrinsicProvider {
    private const int MaxTracks = 4_096;
    private const long MaxCells = 1_000_000;

    private readonly Dictionary<Node, GridPlacement> _explicitPlacements =
        new(ReferenceEqualityComparer.Instance);

    public GridLayout()
        : base("GridLayout") {
        IsRelayoutBoundary = true;
    }

    [MarkupAttribute("columns", MarkupValueKind.Integer)]
    public int Columns { get; private set; } = 1;

    [MarkupAttribute("rows", MarkupValueKind.Integer)]
    public int Rows { get; private set; }

    [MarkupAttribute("column-gap", MarkupValueKind.Length)]
    public Length ColumnGap { get; private set; } = Length.Space(1);

    [MarkupAttribute("row-gap", MarkupValueKind.Length)]
    public Length RowGap { get; private set; } = Length.Space(1);

    bool ILayoutViewportDependency.UsesViewportUnits =>
        ColumnGap.UsesViewportUnits || RowGap.UsesViewportUnits;

    [MarkupAttribute("align-items", MarkupValueKind.Enum)]
    public LayoutAlignment AlignItems { get; private set; } = LayoutAlignment.Stretch;

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "columns":
                SetColumns(Math.Max(1, ControlValueParser.ReadInteger(value) ?? Columns));
                break;
            case "rows":
                SetRows(Math.Max(0, ControlValueParser.ReadInteger(value) ?? Rows));
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
            case "align-items":
                AlignItems = ControlValueParser.ReadLayoutAlignment(value, AlignItems);
                break;
        }
    }

    /// <summary>
    /// Sets a placement owned by this grid. The value overrides any declarative
    /// <c>grid-*</c> style on the child and is released when the child leaves
    /// this grid.
    /// </summary>
    public void SetPlacement(Element child, GridPlacement placement) {
        ArgumentNullException.ThrowIfNull(child);
        ValidatePlacement(placement);
        if (!ReferenceEquals(child.Parent, this)) {
            throw new InvalidOperationException(
                "A grid placement can only be set for a direct child of this grid.");
        }

        if (_explicitPlacements.TryGetValue(child, out var current) &&
            current == placement) {
            return;
        }

        _explicitPlacements[child] = placement;
        InvalidateLayout();
    }

    /// <summary>
    /// Removes an explicit placement and restores the child's declarative
    /// <c>grid-*</c> values, if any.
    /// </summary>
    public bool ClearPlacement(Element child) {
        ArgumentNullException.ThrowIfNull(child);
        if (!_explicitPlacements.Remove(child)) {
            return false;
        }

        InvalidateLayout();
        return true;
    }

    protected internal override void OnChildRemoved(Node child) {
        _explicitPlacements.Remove(child);
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
        var childrenHeight = MeasureChildren(
            context,
            childConstraint,
            style.Padding);
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
        var members = context.GetVisibleChildren(this);
        if (members.Count == 0) {
            return;
        }

        var columns = Math.Max(1, Columns);
        var columnGap = ResolveGap(ColumnGap, context);
        var rowGap = ResolveGap(RowGap, context);
        var columnWidth = Math.Max(
            0,
            (content.Width - (columnGap * (columns - 1))) / columns);
        var placements = new List<(
            Node Child,
            ResolvedStyle Style,
            int Row,
            int Column,
            int RowSpan,
            int ColumnSpan,
            LayoutSize Measured)>(members.Count);
        var rowHeights = new Dictionary<int, float>();
        long nextAutoIndex = 0;
        long maxRow = -1;

        foreach (var child in members) {
            var style = context.GetStyle(child);
            var placement = ResolvePlacement(
                child,
                style,
                ref nextAutoIndex,
                columns);
            var cellWidth = (columnWidth * placement.ColumnSpan) +
                (columnGap * (placement.ColumnSpan - 1));
            var measured = context.Measure(
                child,
                LayoutConstraints.Loose(cellWidth, content.Height));
            placements.Add((
                child,
                style,
                placement.Row,
                placement.Column,
                placement.RowSpan,
                placement.ColumnSpan,
                measured));
            rowHeights[placement.Row] = Math.Max(
                rowHeights.GetValueOrDefault(placement.Row),
                measured.Height);
            maxRow = Math.Max(
                maxRow,
                (long)placement.Row + placement.RowSpan - 1);
        }

        var rowCount = Rows > 0 ? Rows : checked((int)(maxRow + 1));
        EnsureGridSize(columns, rowCount);
        var rowHeight = Rows > 0 && rowCount > 0
            ? Math.Max(0, (content.Height - (rowGap * (rowCount - 1))) / rowCount)
            : 0;
        var rowOffsets = new float[checked(rowCount + 1)];
        for (var row = 0; row < rowCount; row++) {
            var height = rowHeight > 0
                ? rowHeight
                : rowHeights.GetValueOrDefault(row, context.Metrics.Space(8));
            rowOffsets[row + 1] = rowOffsets[row] + height + rowGap;
        }

        foreach (var placement in placements) {
            var cellWidth = (columnWidth * placement.ColumnSpan) +
                (columnGap * (placement.ColumnSpan - 1));
            var rowEnd = checked(placement.Row + placement.RowSpan);
            var cellHeight = rowOffsets[Math.Min(rowCount, rowEnd)] -
                rowOffsets[Math.Min(rowCount, placement.Row)] -
                rowGap;
            var cellX = content.X + (placement.Column * (columnWidth + columnGap));
            var cellY = content.Y + rowOffsets[Math.Min(rowCount, placement.Row)];
            var availableWidth = Math.Max(
                0,
                cellWidth - (placement.Style.Margin * 2));
            var availableHeight = Math.Max(
                0,
                cellHeight - (placement.Style.Margin * 2));
            var childWidth = Math.Min(
                availableWidth,
                Math.Max(0, placement.Measured.Width - (placement.Style.Margin * 2)));
            var childHeight = Math.Min(
                availableHeight,
                Math.Max(0, placement.Measured.Height - (placement.Style.Margin * 2)));
            var alignment = placement.Style.AlignSelf == LayoutAlignment.Auto
                ? AlignItems
                : placement.Style.AlignSelf;
            if (alignment == LayoutAlignment.Stretch) {
                childWidth = availableWidth;
                childHeight = availableHeight;
            }

            var x = AlignInCell(
                cellX,
                cellWidth,
                childWidth,
                placement.Style.Margin,
                alignment,
                placement.Style.Anchor,
                horizontal: true);
            var y = AlignInCell(
                cellY,
                cellHeight,
                childHeight,
                placement.Style.Margin,
                alignment,
                placement.Style.Anchor,
                horizontal: false);
            context.ArrangeChild(
                placement.Child,
                new Rect(x, y, childWidth, childHeight));
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
        var children = context.GetVisibleChildren(this);
        if (children.Count == 0) {
            return 0;
        }

        var columns = Math.Max(1, Columns);
        var columnGap = ResolveGap(ColumnGap, context);
        var rowGap = ResolveGap(RowGap, context);
        var columnWidth = Math.Max(
            0,
            (constraint.MaxWidth - (columnGap * (columns - 1))) / columns);
        var rowHeights = new Dictionary<int, float>();
        long nextAutoIndex = 0;
        var maxRow = -1;

        foreach (var child in children) {
            var style = context.GetStyle(child);
            var placement = ResolvePlacement(
                child,
                style,
                ref nextAutoIndex,
                columns);
            var childConstraint = LayoutConstraints.Loose(
                Math.Min(
                    constraint.MaxWidth,
                    (columnWidth * placement.ColumnSpan) +
                    (columnGap * (placement.ColumnSpan - 1))),
                constraint.MaxHeight);
            var measured = context.Measure(child, childConstraint);
            rowHeights[placement.Row] = Math.Max(
                rowHeights.GetValueOrDefault(placement.Row),
                measured.Height);
            maxRow = Math.Max(
                maxRow,
                checked(placement.Row + placement.RowSpan - 1));
        }

        var height = 0f;
        EnsureGridSize(columns, checked(maxRow + 1));
        for (var row = 0; row <= maxRow; row++) {
            height += rowHeights.GetValueOrDefault(row, context.Metrics.Space(8));
            if (row < maxRow) {
                height += rowGap;
            }
        }

        return height + (padding * 2);
    }

    private float MeasureIntrinsicCore(
        LayoutMeasureContext context,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent) {
        var style = context.GetStyle(this);
        var children = context.GetVisibleChildren(this);
        var columns = Math.Max(1, Columns);
        var horizontal = axis == LayoutAxis.Horizontal;
        var gap = horizontal
            ? ResolveGap(ColumnGap, context)
            : ResolveGap(RowGap, context);
        if (children.Count == 0) {
            return style.Padding * 2;
        }

        var trackExtents = new Dictionary<int, float>();
        long nextAutoIndex = 0;
        var maxTrack = -1;

        foreach (var child in children) {
            var childStyle = context.GetStyle(child);
            var placement = ResolvePlacement(
                child,
                childStyle,
                ref nextAutoIndex,
                columns);
            var start = horizontal ? placement.Column : placement.Row;
            var span = horizontal
                ? placement.ColumnSpan
                : placement.RowSpan;
            var extent = context.MeasureIntrinsic(
                child,
                axis,
                mode,
                crossAxisExtent);
            var distributed = Math.Max(
                0,
                (extent - (gap * (span - 1))) / span);
            for (var track = start; track < checked(start + span); track++) {
                trackExtents[track] = Math.Max(
                    trackExtents.GetValueOrDefault(track),
                    distributed);
            }

            maxTrack = Math.Max(maxTrack, checked(start + span - 1));
        }

        var total = 0f;
        for (var track = 0; track <= maxTrack; track++) {
            total += trackExtents.GetValueOrDefault(track);
        }

        total += gap * maxTrack;
        return total + (style.Padding * 2);
    }

    private GridChildPlacement ResolvePlacement(
        Node child,
        ResolvedStyle style,
        ref long nextAutoIndex,
        int columns) {
        var rowLimit = Rows > 0 ? Rows : MaxTracks;
        int row;
        int column;
        int rowSpan;
        int columnSpan;
        if (_explicitPlacements.TryGetValue(child, out var explicitPlacement)) {
            row = explicitPlacement.Row;
            column = explicitPlacement.Column;
            rowSpan = explicitPlacement.RowSpan;
            columnSpan = explicitPlacement.ColumnSpan;
            ValidateResolvedPlacement(
                row,
                column,
                rowSpan,
                columnSpan,
                columns,
                rowLimit);
        } else {
            row = style.GridRow >= 0
                ? Math.Min(style.GridRow, rowLimit - 1)
                : checked((int)(nextAutoIndex / columns));
            column = style.GridColumn >= 0
                ? Math.Min(style.GridColumn, columns - 1)
                : checked((int)(nextAutoIndex % columns));
            rowSpan = Math.Max(1, style.GridRowSpan);
            columnSpan = Math.Max(1, style.GridColumnSpan);
            rowSpan = Math.Clamp(rowSpan, 1, Math.Max(1, rowLimit - row));
            columnSpan = Math.Clamp(columnSpan, 1, columns - column);

            var cellBudgetSpan = Math.Max(1, MaxCells / columnSpan);
            rowSpan = Math.Clamp(
                rowSpan,
                1,
                (int)Math.Min(Math.Max(1, rowLimit - row), cellBudgetSpan));

            var declarativeEnd = checked(((long)row * columns) + column + columnSpan);
            if (declarativeEnd > MaxCells) {
                row = checked((int)(nextAutoIndex / columns));
                column = checked((int)(nextAutoIndex % columns));
                rowSpan = Math.Clamp(
                    Math.Max(1, style.GridRowSpan),
                    1,
                    Math.Max(1, rowLimit - row));
                columnSpan = Math.Clamp(
                    Math.Max(1, style.GridColumnSpan),
                    1,
                    columns - column);
            }
        }

        ValidateResolvedPlacement(
            row,
            column,
            rowSpan,
            columnSpan,
            columns,
            rowLimit);

        var next = Math.Max(
            checked(nextAutoIndex + columnSpan),
            checked(((long)row * columns) + column + columnSpan));
        if (next > MaxCells) {
            throw new ArgumentOutOfRangeException(
                nameof(nextAutoIndex),
                "The grid would contain too many cells.");
        }

        nextAutoIndex = next;
        return new GridChildPlacement(row, column, rowSpan, columnSpan);
    }

    private void ValidatePlacement(GridPlacement placement) {
        if (placement.Row < 0 ||
            placement.Column < 0 ||
            placement.RowSpan < 1 ||
            placement.ColumnSpan < 1) {
            throw new ArgumentOutOfRangeException(
                nameof(placement),
                "Grid placement values must be non-negative and spans must be positive.");
        }

        var rowEnd = checked((long)placement.Row + placement.RowSpan);
        var columnEnd = checked((long)placement.Column + placement.ColumnSpan);
        var rowLimit = Rows > 0 ? Rows : MaxTracks;
        if (rowEnd > MaxTracks ||
            columnEnd > MaxTracks ||
            rowEnd > rowLimit ||
            columnEnd > Columns ||
            checked((long)placement.RowSpan * placement.ColumnSpan) > MaxCells) {
            throw new ArgumentOutOfRangeException(
                nameof(placement),
                "The grid placement exceeds the supported track or cell limit.");
        }
    }

    private static void ValidateResolvedPlacement(
        int row,
        int column,
        int rowSpan,
        int columnSpan,
        int columns,
        int rowLimit) {
        if (row < 0 ||
            column < 0 ||
            column >= columns ||
            rowSpan < 1 ||
            columnSpan < 1 ||
            (long)row + rowSpan > MaxTracks ||
            (long)row + rowSpan > rowLimit ||
            (long)column + columnSpan > columns ||
            checked((long)rowSpan * columnSpan) > MaxCells) {
            throw new ArgumentOutOfRangeException(
                nameof(column),
                "The child placement exceeds the supported grid bounds.");
        }
    }

    private void SetColumns(int value) {
        ValidateTrackCount(value, nameof(Columns));
        EnsureGridSize(value, Rows);
        var previous = Columns;
        Columns = value;
        try {
            foreach (var placement in _explicitPlacements.Values) {
                ValidatePlacement(placement);
            }
        } catch {
            Columns = previous;
            throw;
        }
    }

    private void SetRows(int value) {
        ValidateTrackCount(value, nameof(Rows));
        EnsureGridSize(Columns, value);
        var previous = Rows;
        Rows = value;
        try {
            foreach (var placement in _explicitPlacements.Values) {
                ValidatePlacement(placement);
            }
        } catch {
            Rows = previous;
            throw;
        }
    }

    private static void ValidateTrackCount(int value, string parameterName) {
        if (value < 0 || value > MaxTracks) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The track count must be between 0 and {MaxTracks}.");
        }
    }

    private static void EnsureGridSize(int columns, int rows) {
        if (columns < 0 || rows < 0 ||
            checked((long)columns * rows) > MaxCells) {
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                "The grid dimensions exceed the supported cell limit.");
        }
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

    private static float AlignInCell(
        float start,
        float size,
        float childSize,
        float margin,
        LayoutAlignment alignSelf,
        Anchor anchor,
        bool horizontal) {
        if (alignSelf != LayoutAlignment.Auto) {
            return alignSelf switch {
                LayoutAlignment.Center => start + ((size - childSize) / 2),
                LayoutAlignment.End => start + size - childSize - margin,
                _ => start + margin,
            };
        }

        return horizontal
            ? AlignX(new Rect(start, start, size, size), childSize, anchor, margin)
            : AlignY(new Rect(start, start, size, size), childSize, anchor, margin);
    }

    private static float AlignX(
        Rect content,
        float width,
        Anchor anchor,
        float margin) {
        return anchor switch {
            Anchor.Top or Anchor.Center or Anchor.Bottom =>
                content.X + ((content.Width - width) / 2),
            Anchor.TopRight or Anchor.Right or Anchor.BottomRight =>
                content.Right - width - margin,
            _ => content.X + margin,
        };
    }

    private static float AlignY(
        Rect content,
        float height,
        Anchor anchor,
        float margin) {
        return anchor switch {
            Anchor.Left or Anchor.Center or Anchor.Right =>
                content.Y + ((content.Height - height) / 2),
            Anchor.BottomLeft or Anchor.Bottom or Anchor.BottomRight =>
                content.Bottom - height - margin,
            _ => content.Y + margin,
        };
    }

}
