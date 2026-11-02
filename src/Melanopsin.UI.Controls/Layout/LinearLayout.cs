using System.Buffers;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public class LinearLayout :
    Control,
    ILinearLayoutContainer,
    ILayoutParticipant,
    ILayoutViewportDependency,
    ILayoutIntrinsicProvider {
    public LinearLayout()
        : this("LinearLayout") {
    }

    protected LinearLayout(string type)
        : base(type) {
        IsRelayoutBoundary = true;
    }

    [MarkupAttribute("orientation", MarkupValueKind.Enum)]
    public StackOrientation Orientation { get; private set; } = StackOrientation.Vertical;

    [MarkupAttribute("spacing", MarkupValueKind.Length)]
    public Length Spacing { get; private set; } = Length.Su(1);

    bool ILayoutViewportDependency.UsesViewportUnits =>
        Spacing.UsesViewportUnits;

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "orientation":
                Orientation = value is StackOrientation orientation
                    ? orientation
                    : StackOrientation.Vertical;
                break;
            case "spacing":
                if (value is Length spacing) {
                    Spacing = spacing;
                }

                break;
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
        var childConstraints = LayoutConstraints.Loose(
            Math.Max(
                0,
                width - (style.Padding * 2) - (style.Margin * 2)),
            constraints.HasBoundedHeight
                ? Math.Max(0, constraints.MaxHeight - (style.Padding * 2))
                : float.PositiveInfinity);
        var children = context.GetVisibleChildren(this);
        var horizontal = Orientation == StackOrientation.Horizontal;
        var spacing = LayoutMath.ResolveGap(Spacing, context.Metrics);
        var childrenWidth = 0f;
        var childrenHeight = 0f;

        for (var index = 0; index < children.Count; index++) {
            var child = children[index];
            var measured = context.Measure(child, childConstraints);
            var childStyle = context.GetStyle(child);
            var weighted = childStyle.Weight > 0;
            if (horizontal) {
                if (!weighted) {
                    childrenWidth += measured.Width;
                }

                childrenWidth += index == 0 ? 0 : spacing;
                childrenHeight = Math.Max(childrenHeight, measured.Height);
            } else {
                childrenWidth = Math.Max(childrenWidth, measured.Width);
                if (!weighted) {
                    childrenHeight += measured.Height;
                }

                childrenHeight += index == 0 ? 0 : spacing;
            }
        }

        _ = childrenWidth;
        var fallbackHeight = Math.Max(
            context.Metrics.Space(8),
            childrenHeight + (style.Padding * 2));
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
        var content = context.ContentBounds;
        var children = context.GetVisibleChildren(this);
        if (children.Count == 0) {
            return;
        }

        var count = children.Count;
        var measured = ArrayPool<LayoutSize>.Shared.Rent(count);
        ResolvedStyle[]? styles = null;
        try {
            styles = ArrayPool<ResolvedStyle>.Shared.Rent(count);
            var horizontal = Orientation == StackOrientation.Horizontal;
            var spacing = LayoutMath.ResolveGap(Spacing, context.Metrics);
            var totalWeight = 0f;
            var consumed = 0f;

            for (var index = 0; index < count; index++) {
                measured[index] = context.Measure(
                    children[index],
                    LayoutConstraints.Loose(content.Width, content.Height));
                styles[index] = context.GetStyle(children[index]);
                totalWeight += Math.Max(0, styles[index].Weight);
                if (styles[index].Weight <= 0) {
                    consumed += horizontal
                        ? measured[index].Width
                        : measured[index].Height;
                } else {
                    consumed += styles[index].Margin * 2;
                }

                consumed += index == 0 ? 0 : spacing;
            }

            var available = horizontal ? content.Width : content.Height;
            var free = Math.Max(0, available - consumed);
            var cursor = horizontal ? content.X : content.Y;

            for (var index = 0; index < count; index++) {
                var child = children[index];
                var childStyle = styles[index];
                var weightShare = totalWeight > 0
                    ? free * (Math.Max(0, childStyle.Weight) / totalWeight)
                    : 0;
                var childWidth = childStyle.Weight > 0 && horizontal
                    ? 0
                    : Math.Max(
                        0,
                        measured[index].Width - (childStyle.Margin * 2));
                var childHeight = childStyle.Weight > 0 && !horizontal
                    ? 0
                    : Math.Max(
                        0,
                        measured[index].Height - (childStyle.Margin * 2));

                if (horizontal) {
                    childWidth += weightShare;
                    var y = LayoutMath.AlignY(
                        content,
                        childHeight,
                        childStyle.Anchor,
                        childStyle.Margin);
                    var childBounds = new Rect(
                        cursor + childStyle.Margin,
                        y,
                        childWidth,
                        childHeight);
                    cursor += childWidth + (childStyle.Margin * 2) +
                        (index == count - 1 ? 0 : spacing);
                    context.ArrangeChild(child, childBounds);
                } else {
                    childHeight += weightShare;
                    var x = LayoutMath.AlignX(
                        content,
                        childWidth,
                        childStyle.Anchor,
                        childStyle.Margin);
                    var childBounds = new Rect(
                        x,
                        cursor + childStyle.Margin,
                        childWidth,
                        childHeight);
                    cursor += childHeight + (childStyle.Margin * 2) +
                        (index == count - 1 ? 0 : spacing);
                    context.ArrangeChild(child, childBounds);
                }
            }
        } finally {
            ArrayPool<LayoutSize>.Shared.Return(measured);
            if (styles is not null) {
                ArrayPool<ResolvedStyle>.Shared.Return(styles, clearArray: true);
            }
        }
    }

    float ILayoutIntrinsicProvider.MeasureIntrinsic(
        LayoutMeasureContext context,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent) {
        var children = context.GetVisibleChildren(this);
        var style = context.GetStyle(this);
        var spacing = LayoutMath.ResolveGap(Spacing, context.Metrics);
        var horizontal = Orientation == StackOrientation.Horizontal;
        var extent = 0f;

        if (axis == LayoutAxis.Horizontal) {
            if (horizontal) {
                for (var index = 0; index < children.Count; index++) {
                    extent += context.MeasureIntrinsic(
                        children[index],
                        axis,
                        mode,
                        crossAxisExtent);
                    if (index > 0) {
                        extent += spacing;
                    }
                }
            } else {
                foreach (var child in children) {
                    extent = Math.Max(
                        extent,
                        context.MeasureIntrinsic(
                            child,
                            axis,
                            mode,
                            crossAxisExtent));
                }
            }

            return extent + (style.Padding * 2);
        }

        if (horizontal) {
            foreach (var child in children) {
                extent = Math.Max(
                    extent,
                    context.MeasureIntrinsic(
                        child,
                        axis,
                        mode,
                        crossAxisExtent));
            }
        } else {
            for (var index = 0; index < children.Count; index++) {
                extent += context.MeasureIntrinsic(
                    children[index],
                    axis,
                    mode,
                    crossAxisExtent);
                if (index > 0) {
                    extent += spacing;
                }
            }
        }

        return extent + (style.Padding * 2);
    }

}
