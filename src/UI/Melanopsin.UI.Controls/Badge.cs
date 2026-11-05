// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Badge :
    Control,
    ISingleLineTextContent,
    ILayoutFallbackSizeProvider {
    private string _text = string.Empty;

    public Badge()
        : base("Badge") {
        Accessibility.Role = AccessibilityRole.Status;
    }

    [MarkupAttribute("text")]
    public string Text {
        get => _text;
        set {
            if (string.Equals(_text, value, StringComparison.Ordinal)) {
                return;
            }

            _text = value;
            Accessibility.Name = value;
            InvalidateLayout();
        }
    }

    [MarkupAttribute("tone")]
    public string? Tone { get; private set; }

    public void AppendText(string value) {
        Text += value;
    }

    float ILayoutFallbackSizeProvider.MeasureFallbackHeight(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        return context.Metrics.Space(7);
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "text":
                Text = value?.ToString() ?? string.Empty;
                break;
            case "tone":
                Tone = value?.ToString();
                break;
        }
    }
}
