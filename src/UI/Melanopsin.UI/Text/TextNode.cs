// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

/// <summary>
/// A leaf node containing display text.
/// </summary>
public sealed class TextNode :
    Node,
    ILayoutIntrinsicProvider,
    ILayoutViewportDependency {
    internal TextNode(string text) {
        Text = text;
    }

    public string Text { get; }

    bool ILayoutViewportDependency.UsesViewportUnits => false;

    float ILayoutIntrinsicProvider.MeasureIntrinsic(
        LayoutMeasureContext context,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent) {
        var fontSize = context.Metrics.Font(UiMetrics.DefaultFontSize);
        if (axis == LayoutAxis.Vertical) {
            var availableWidth = float.IsFinite(crossAxisExtent)
                ? crossAxisExtent
                : float.PositiveInfinity;
            var metrics = context.MeasureText(Text, fontSize, availableWidth);
            return Math.Max(context.Metrics.Space(6), metrics.Height);
        }

        if (mode == LayoutIntrinsicMode.Maximum) {
            return context.MeasureText(Text, fontSize).Width;
        }

        var minimum = 0f;
        foreach (var token in Text.Split(
                     (char[]?)null,
                     StringSplitOptions.RemoveEmptyEntries)) {
            minimum = Math.Max(
                minimum,
                context.MeasureText(token, fontSize).Width);
        }

        return minimum;
    }
}
