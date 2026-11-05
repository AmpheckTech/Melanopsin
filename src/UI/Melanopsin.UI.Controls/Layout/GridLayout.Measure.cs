// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Controls;

public sealed partial class GridLayout {
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
        var columnGap = LayoutMath.ResolveGap(ColumnGap, context.Metrics);
        var rowGap = LayoutMath.ResolveGap(RowGap, context.Metrics);
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
            ? LayoutMath.ResolveGap(ColumnGap, context.Metrics)
            : LayoutMath.ResolveGap(RowGap, context.Metrics);
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
}
