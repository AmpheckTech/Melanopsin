// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class TextBlock :
    Control,
    ITextContentControl,
    ITextContentWrapPolicy,
    ILayoutFallbackSizeProvider {
    private string _text = string.Empty;
    private bool _wrapText = true;

    public TextBlock()
        : base("TextBlock") {
        Accessibility.Role = AccessibilityRole.Text;
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

    public void AppendText(string value) {
        Text += value;
    }

    [MarkupAttribute("wrap", MarkupValueKind.Boolean)]
    public bool WrapText {
        get => _wrapText;
        private set {
            if (_wrapText == value) {
                return;
            }

            _wrapText = value;
            InvalidateLayout();
        }
    }

    float ILayoutFallbackSizeProvider.MeasureFallbackHeight(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        return context.Metrics.Space(6);
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "text") {
            Text = value as string ?? string.Empty;
        } else if (name == "wrap") {
            WrapText = value is bool wrap ? wrap : WrapText;
        }
    }
}
