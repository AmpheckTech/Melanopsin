// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Layout;

/// <summary>
/// Engine services available while a layout participant measures itself and
/// its children.
/// </summary>
public sealed class LayoutMeasureContext {
    private readonly FlowLayoutEngine _engine;

    internal LayoutMeasureContext(FlowLayoutEngine engine) {
        _engine = engine;
    }

    public UiMetrics Metrics => _engine.Metrics;

    public LayoutSize Measure(Node node, LayoutConstraints constraints) {
        return _engine.MeasureNode(node, constraints);
    }

    public float MeasureIntrinsic(
        Node node,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent = float.PositiveInfinity) {
        return _engine.MeasureIntrinsic(
            node,
            axis,
            mode,
            crossAxisExtent);
    }

    public TextMetrics MeasureText(
        string text,
        float fontSize,
        float availableWidth = float.PositiveInfinity,
        FontFace? font = null) {
        return _engine.MeasureText(text, fontSize, availableWidth, font);
    }

    public ResolvedStyle GetStyle(Node node) {
        return _engine.ResolveStyle(node);
    }

    public IReadOnlyList<Node> GetVisibleChildren(Element element) {
        return _engine.GetVisibleChildren(element);
    }

    public float ResolveWidth(
        ResolvedStyle style,
        LayoutConstraints constraints,
        float fallback) {
        var available = constraints.HasBoundedWidth
            ? constraints.MaxWidth
            : constraints.WidthReference ?? fallback;
        return constraints.ConstrainWidth(_engine.ResolveLength(
            style.Width,
            style.MinWidth,
            style.MaxWidth,
            available,
            fallback,
            style.FontSize,
            clampToAvailable: false));
    }

    public float ResolveHeight(
        ResolvedStyle style,
        LayoutConstraints constraints,
        float fallback) {
        var available = constraints.HasBoundedHeight
            ? constraints.MaxHeight
            : constraints.HeightReference ?? fallback;
        return constraints.ConstrainHeight(_engine.ResolveLength(
            style.Height,
            style.MinHeight,
            style.MaxHeight,
            available,
            fallback,
            style.FontSize,
            clampToAvailable: false));
    }
}
