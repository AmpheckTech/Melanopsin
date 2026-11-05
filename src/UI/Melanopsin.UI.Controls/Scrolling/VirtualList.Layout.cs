// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Controls;

public sealed partial class VirtualList {
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
            Math.Max(context.Metrics.Space(12), RowHeight * 4));
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
                    content.MinX,
                    content.MinY + _rowLayout.GetRowTop(index),
                    content.Width,
                    height));
        }
    }
}
