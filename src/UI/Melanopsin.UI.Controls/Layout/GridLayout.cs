using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed partial class GridLayout :
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
    public Length ColumnGap { get; private set; } = Length.Su(1);

    [MarkupAttribute("row-gap", MarkupValueKind.Length)]
    public Length RowGap { get; private set; } = Length.Su(1);

    bool ILayoutViewportDependency.UsesViewportUnits =>
        ColumnGap.UsesViewportUnits || RowGap.UsesViewportUnits;

    [MarkupAttribute("align-items", MarkupValueKind.Enum)]
    public LayoutAlignment AlignItems { get; private set; } = LayoutAlignment.Stretch;

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "columns":
                SetColumns(Math.Max(1, value is int columns ? columns : Columns));
                break;
            case "rows":
                SetRows(Math.Max(0, value is int rows ? rows : Rows));
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
            case "align-items":
                AlignItems = value is LayoutAlignment alignItems ? alignItems : AlignItems;
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
            ? LayoutMath.AlignX(
                new Rect(start, start, size, size),
                childSize,
                anchor,
                margin)
            : LayoutMath.AlignY(
                new Rect(start, start, size, size),
                childSize,
                anchor,
                margin);
    }

}
