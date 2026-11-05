// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Styling;

public sealed class StyleRule {
    public StyleRule(StyleSelector selector, StyleProperties properties) {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(properties);
        Selector = selector;
        Properties = properties;
    }

    public StyleSelector Selector { get; }

    public StyleProperties Properties { get; }

    public StyleSpecificity Specificity => Selector.Specificity;
}
