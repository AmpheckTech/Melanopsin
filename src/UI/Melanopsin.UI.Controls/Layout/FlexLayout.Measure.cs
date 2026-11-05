// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Controls;

public sealed partial class FlexLayout {
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
        var crossAvailable = rowAxis ? constraint.MaxHeight : constraint.MaxWidth;
        var columnGap = LayoutMath.ResolveGap(ColumnGap, context.Metrics);
        var rowGap = LayoutMath.ResolveGap(RowGap, context.Metrics);
        var gap = rowAxis ? columnGap : rowGap;
        var crossGap = rowAxis ? rowGap : columnGap;
        var count = children.Count;
        var measured = ArrayPool<LayoutSize>.Shared.Rent(count);
        ResolvedStyle[]? styles = null;
        try {
            styles = ArrayPool<ResolvedStyle>.Shared.Rent(count);
            for (var index = 0; index < count; index++) {
                measured[index] = context.Measure(children[index], constraint);
                styles[index] = context.GetStyle(children[index]);
            }

            var totalCross = 0f;
            var lineStart = 0;
            var lineMain = 0f;
            for (var index = 0; index < count; index++) {
                var childMain = MainSize(measured[index], rowAxis);
                var required = lineMain == 0 ? childMain : lineMain + gap + childMain;
                if (Wrap == FlexWrap.NoWrap ||
                    lineMain <= 0 ||
                    required <= mainAvailable) {
                    lineMain = required;
                    continue;
                }

                totalCross += lineStart == 0 ? 0 : crossGap;
                totalCross += MeasureLineCross(
                    context,
                    children,
                    measured,
                    styles,
                    lineStart,
                    index - lineStart,
                    rowAxis,
                    mainAvailable,
                    crossAvailable,
                    gap);
                lineStart = index;
                lineMain = childMain;
            }

            totalCross += lineStart == 0 ? 0 : crossGap;
            totalCross += MeasureLineCross(
                context,
                children,
                measured,
                styles,
                lineStart,
                count - lineStart,
                rowAxis,
                mainAvailable,
                crossAvailable,
                gap);
            return totalCross + (padding * 2);
        } finally {
            ArrayPool<LayoutSize>.Shared.Return(measured);
            if (styles is not null) {
                ArrayPool<ResolvedStyle>.Shared.Return(styles, clearArray: true);
            }
        }
    }

    private float MeasureLineCross(
        LayoutMeasureContext context,
        IReadOnlyList<Node> children,
        LayoutSize[] measured,
        ResolvedStyle[] styles,
        int start,
        int count,
        bool rowAxis,
        float mainAvailable,
        float crossAvailable,
        float gap) {
        if (count == 0) {
            return 0;
        }

        var end = start + count;
        var totalMain = 0f;
        var totalGrow = 0f;
        var totalShrink = 0f;
        for (var index = start; index < end; index++) {
            var baseMain = MainSize(measured[index], rowAxis);
            var item = ResolveItem(children[index], styles[index]);
            totalMain += baseMain;
            totalGrow += item.Grow;
            totalShrink += item.Shrink * baseMain;
        }

        totalMain += gap * (count - 1);
        var free = mainAvailable - totalMain;
        var shrinking = free < 0 && totalShrink > 0;
        var growing = free > 0 && totalGrow > 0;
        var lineCross = 0f;
        for (var index = start; index < end; index++) {
            var style = styles[index];
            var item = ResolveItem(children[index], style);
            var baseMain = MainSize(measured[index], rowAxis);
            var targetMain = baseMain;
            if (shrinking) {
                targetMain = Math.Max(
                    0,
                    baseMain - (-free * (item.Shrink * baseMain / totalShrink)));
            } else if (growing) {
                targetMain = baseMain + (free * (item.Grow / totalGrow));
            }

            var cross = CrossSize(measured[index], rowAxis);
            if (float.IsFinite(targetMain) &&
                Math.Abs(targetMain - baseMain) > 0.5f) {
                cross = CrossSize(
                    context.Measure(
                        children[index],
                        AimConstraints(rowAxis, targetMain, crossAvailable)),
                    rowAxis);
            }

            lineCross = Math.Max(lineCross, cross);
        }

        return lineCross;
    }

    private static LayoutConstraints AimConstraints(
        bool rowAxis,
        float mainExtent,
        float crossAvailable) {
        return rowAxis
            ? new LayoutConstraints(mainExtent, mainExtent, 0, crossAvailable)
            : new LayoutConstraints(0, crossAvailable, mainExtent, mainExtent);
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
            ? LayoutMath.ResolveGap(
                horizontal ? ColumnGap : RowGap,
                context.Metrics)
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

}
