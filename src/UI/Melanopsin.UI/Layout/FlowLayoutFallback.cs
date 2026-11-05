using System.Buffers;
using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Layout;

internal sealed class FlowLayoutFallback {
    private readonly FlowLayoutEngine _engine;
    private readonly ITextMeasurer _textMeasurer;

    public FlowLayoutFallback(
        FlowLayoutEngine engine,
        ITextMeasurer textMeasurer) {
        _engine = engine;
        _textMeasurer = textMeasurer;
    }

    public LayoutSize Measure(
        Element element,
        LayoutConstraints constraint) {
        var style = _engine.ResolveStyle(element);
        var widthFallback = constraint.HasBoundedWidth
            ? constraint.MaxWidth
            : 0;
        var contentWidth = _engine.ResolveLength(
            style.Width,
            style.MinWidth,
            style.MaxWidth,
            constraint.MaxWidth,
            widthFallback,
            style.FontSize);
        var width = constraint.ConstrainWidth(
            contentWidth + (style.Margin * 2));
        var childConstraint = LayoutConstraints.Loose(
            Math.Max(0, width - (style.Padding * 2) - (style.Margin * 2)),
            Math.Max(0, constraint.MaxHeight - (style.Padding * 2)));

        var (_, childrenHeight) = MeasureLinearChildren(
            element,
            childConstraint,
            style.Padding);

        var availableTextWidth = element is ISingleLineTextContent
            ? float.PositiveInfinity
            : Math.Max(1, width - (style.Padding * 2));
        var defaultHeight = ResolveFallbackHeight(element, constraint);
        if (element is ITextContentControl { Text.Length: > 0 } textContent) {
            var textMetrics = _engine.MeasureText(
                textContent.Text,
                style.FontSize,
                availableTextWidth,
                style.Font);
            defaultHeight = Math.Max(
                defaultHeight,
                textMetrics.Height + (style.Padding * 2));
        }

        if (element is ICustomTextMeasurer customText &&
            _textMeasurer is ITextLayout textLayout) {
            var textMetrics = customText.MeasureTextLayout(
                textLayout,
                availableTextWidth,
                style.FontSize,
                style.Font);
            defaultHeight = Math.Max(
                defaultHeight,
                textMetrics.Height + (style.Padding * 2));
        }

        var fallbackHeight = Math.Max(defaultHeight, childrenHeight);
        var desiredHeight = _engine.ResolveLength(
            style.Height,
            style.MinHeight,
            style.MaxHeight,
            constraint.MaxHeight,
            fallbackHeight,
            style.FontSize);
        desiredHeight = constraint.ConstrainHeight(
            desiredHeight + (style.Margin * 2));

        return new LayoutSize(width, desiredHeight);
    }

    public void Arrange(
        Element element,
        Rect bounds,
        LayoutNode layoutNode,
        Rect? inheritedClip) {
        var style = _engine.ResolveStyle(element);
        var descendantClip = element is Control { ClipToBounds: true }
            ? bounds
            : inheritedClip;
        if (element is ILayoutParticipant participant) {
            participant.Arrange(
                new LayoutArrangeContext(
                    _engine,
                    layoutNode,
                    bounds,
                    descendantClip,
                    style,
                    _engine.Metrics),
                bounds);
            return;
        }

        var children = _engine.GetVisibleChildren(element);
        if (children.Count == 0) {
            return;
        }

        var content = new Rect(
            bounds.MinX + style.Padding,
            bounds.MinY + style.Padding,
            Math.Max(0, bounds.Width - (style.Padding * 2)),
            Math.Max(0, bounds.Height - (style.Padding * 2)));
        ArrangeLinear(
            element,
            element is ILinearLayoutContainer linear
                ? linear.Orientation
                : StackOrientation.Vertical,
            ResolveSpacing(element),
            content,
            layoutNode,
            descendantClip);
    }

    private float ResolveFallbackHeight(
        Element element,
        LayoutConstraints constraints) {
        return element is ILayoutFallbackSizeProvider provider
            ? provider.MeasureFallbackHeight(
                new LayoutMeasureContext(_engine),
                constraints)
            : _engine.Metrics.Space(8);
    }

    private (float Width, float Height) MeasureLinearChildren(
        Element element,
        LayoutConstraints constraint,
        float padding) {
        var children = _engine.GetVisibleChildren(element);
        if (children.Count == 0) {
            return (0, 0);
        }

        var spacing = ResolveSpacing(element);
        var width = 0f;
        var height = 0f;

        for (var index = 0; index < children.Count; index++) {
            var measured = _engine.MeasureNode(children[index], constraint);
            var childStyle = _engine.ResolveStyle(children[index]);
            var weighted = childStyle.Weight > 0;
            width = Math.Max(width, measured.Width);
            if (!weighted) {
                height += measured.Height;
            }

            height += index == 0 ? 0 : spacing;
        }

        return (width + (padding * 2), height + (padding * 2));
    }

    private void ArrangeLinear(
        Element element,
        StackOrientation orientation,
        float spacing,
        Rect content,
        LayoutNode layoutNode,
        Rect? descendantClip) {
        var children = _engine.GetVisibleChildren(element);
        if (children.Count == 0) {
            return;
        }

        var count = children.Count;
        var measured = ArrayPool<LayoutSize>.Shared.Rent(count);
        ResolvedStyle[]? styles = null;
        try {
            styles = ArrayPool<ResolvedStyle>.Shared.Rent(count);
            var horizontal = orientation == StackOrientation.Horizontal;
            var totalWeight = 0f;
            var consumed = 0f;

            for (var index = 0; index < count; index++) {
                measured[index] = _engine.MeasureNode(
                    children[index],
                    LayoutConstraints.Loose(content.Width, content.Height));
                styles[index] = _engine.ResolveStyle(children[index]);
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
            var cursor = horizontal ? content.MinX : content.MinY;

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
                    layoutNode.Children.Add(_engine.ArrangeNode(
                        child,
                        childBounds,
                        layoutNode,
                        descendantClip));
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
                    layoutNode.Children.Add(_engine.ArrangeNode(
                        child,
                        childBounds,
                        layoutNode,
                        descendantClip));
                }
            }
        } finally {
            ArrayPool<LayoutSize>.Shared.Return(measured);
            if (styles is not null) {
                ArrayPool<ResolvedStyle>.Shared.Return(styles, clearArray: true);
            }
        }
    }

    private float ResolveSpacing(Element element) {
        var metrics = _engine.Metrics;
        return element is ILinearLayoutContainer layout
            ? LayoutMath.ResolveGap(layout.Spacing, metrics)
            : metrics.Space(1);
    }
}
