using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

[MarkupAttribute("class")]
[MarkupAttribute("style")]
[MarkupAttribute("a11y-label")]
[MarkupAttribute("a11y-description")]
[MarkupAttribute("a11y-hidden", MarkupValueKind.Boolean)]
[MarkupAttribute("disabled", MarkupValueKind.Boolean)]
[MarkupAttribute("width", MarkupValueKind.Length)]
[MarkupAttribute("height", MarkupValueKind.Length)]
[MarkupAttribute("min-width", MarkupValueKind.Length)]
[MarkupAttribute("max-width", MarkupValueKind.Length)]
[MarkupAttribute("min-height", MarkupValueKind.Length)]
[MarkupAttribute("max-height", MarkupValueKind.Length)]
[MarkupAttribute("flex-grow", MarkupValueKind.Number)]
[MarkupAttribute("flex-shrink", MarkupValueKind.Number)]
[MarkupAttribute("weight", MarkupValueKind.Number)]
[MarkupAttribute("order", MarkupValueKind.Integer)]
[MarkupAttribute("align-self")]
[MarkupAttribute("grid-row", MarkupValueKind.Integer)]
[MarkupAttribute("grid-column", MarkupValueKind.Integer)]
[MarkupAttribute("grid-row-span", MarkupValueKind.Integer)]
[MarkupAttribute("grid-column-span", MarkupValueKind.Integer)]
[MarkupAttribute("font")]
public abstract class Control : Element {
    private bool _isVisible = true;

    protected Control(string type)
        : base(type) {
    }

    [MarkupAttribute("id")]
    public string? Id { get; private set; }

    [MarkupAttribute("background")]
    public Color? Background { get; private set; }

    [MarkupAttribute("input", MarkupValueKind.Input)]
    public bool IsHitTestVisible { get; private set; } = true;

    [MarkupAttribute("z-index", MarkupValueKind.Integer)]
    public int ZIndex { get; private set; }

    [MarkupAttribute("clip", MarkupValueKind.Boolean)]
    public bool ClipToBounds { get; protected set; }

    [MarkupAttribute("focusable", MarkupValueKind.Boolean)]
    public override bool IsFocusable { get; protected set; }

    [MarkupAttribute("visible", MarkupValueKind.Boolean)]
    public bool IsVisible {
        get => _isVisible;
        private set {
            if (_isVisible == value) {
                return;
            }

            _isVisible = value;
            InvalidateLayout(LayoutInvalidationReason.Structure);
        }
    }

    public bool IsEnabled { get; private set; } = true;

    [MarkupAttribute("anchor", MarkupValueKind.Enum)]
    public Anchor Anchor { get; private set; } = Anchor.TopLeft;

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "id":
                Id = value?.ToString();
                break;
            case "background":
                Background = ControlValueParser.ReadColor(value);
                break;
            case "input":
                IsHitTestVisible = !string.Equals(
                    value?.ToString(),
                    "none",
                    StringComparison.OrdinalIgnoreCase);
                break;
            case "z-index":
                ZIndex = ControlValueParser.ReadInteger(value) ?? 0;
                break;
            case "clip":
                ClipToBounds = ControlValueParser.ReadBoolean(value) ?? false;
                break;
            case "focusable":
                IsFocusable = ControlValueParser.ReadBoolean(value) ?? IsFocusable;
                break;
            case "visible":
                IsVisible = ControlValueParser.ReadBoolean(value) ?? true;
                break;
            case "a11y-label":
                Accessibility.Name = value?.ToString();
                break;
            case "a11y-description":
                Accessibility.Description = value?.ToString();
                break;
            case "a11y-hidden":
                Accessibility.IsHidden = ControlValueParser.ReadBoolean(value) ?? false;
                break;
            case "disabled":
                SetEnabled(!(ControlValueParser.ReadBoolean(value) ?? true));
                break;
            case "anchor":
                Anchor = ControlValueParser.ReadAnchor(value);
                break;
        }
    }

    public void SetVisible(bool visible) {
        IsVisible = visible;
    }

    public void SetEnabled(bool enabled) {
        if (IsEnabled == enabled) {
            return;
        }

        IsEnabled = enabled;
        if (!enabled) {
            SetState(UiStateNames.Pressed, false);
        }

        InvalidatePaint();
    }
}
