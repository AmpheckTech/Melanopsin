using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Icon : Control, ISingleLineTextContent {
    private string _glyph = string.Empty;

    public Icon()
        : base("Icon") {
        Accessibility.Role = AccessibilityRole.Image;
    }

    [MarkupAttribute("glyph")]
    public string Glyph {
        get => _glyph;
        set {
            if (string.Equals(_glyph, value, StringComparison.Ordinal)) {
                return;
            }

            _glyph = value;
            Accessibility.Name = value;
            InvalidateLayout();
        }
    }

    public string Text {
        get => Glyph;
        set => Glyph = value;
    }

    public void AppendText(string value) {
        Glyph += value;
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "glyph") {
            Glyph = value?.ToString() ?? string.Empty;
        }
    }
}
