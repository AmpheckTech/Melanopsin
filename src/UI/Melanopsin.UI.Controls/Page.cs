// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Page : Control, ILayoutFallbackSizeProvider {
    public Page()
        : base("Page") {
        Accessibility.Role = AccessibilityRole.Region;
    }

    float ILayoutFallbackSizeProvider.MeasureFallbackHeight(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        return constraints.HasBoundedHeight
            ? constraints.MaxHeight
            : context.Metrics.Space(8);
    }

    [MarkupAttribute("title")]
    public string? Title { get; private set; }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "title") {
            Title = value?.ToString();
            Accessibility.Name = Title;
        }
    }
}
