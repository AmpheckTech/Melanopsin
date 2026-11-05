// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class ListRow : Control, ILayoutFallbackSizeProvider {
    public ListRow()
        : base("ListRow") {
        Accessibility.Role = AccessibilityRole.ListItem;
    }

    float ILayoutFallbackSizeProvider.MeasureFallbackHeight(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        return context.Metrics.Space(10);
    }
}
