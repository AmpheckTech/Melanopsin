// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Controls;

public sealed partial class GridLayout {
    void ILayoutParticipant.Arrange(
        LayoutArrangeContext context,
        Rect bounds) {
        var content = context.ContentBounds;
        var members = context.GetVisibleChildren(this);
        if (members.Count == 0) {
            return;
        }

        var columns = Math.Max(1, Columns);
        var columnGap = LayoutMath.ResolveGap(ColumnGap, context.Metrics);
        var rowGap = LayoutMath.ResolveGap(RowGap, context.Metrics);
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
            var cellX = content.MinX + (placement.Column * (columnWidth + columnGap));
            var cellY = content.MinY + rowOffsets[Math.Min(rowCount, placement.Row)];
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
}
