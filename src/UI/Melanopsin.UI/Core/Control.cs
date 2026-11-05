// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

[MarkupAttribute("class")]
[MarkupAttribute("style")]
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

    [MarkupAttribute("id")]
    public static readonly ElementAttribute<string?> IdAttribute = new(
        "id",
        MarkupValueKind.Any,
        static (element, value) => ((Control)element).Id = value,
        static element => ((Control)element).Id = null,
        InvalidationKind.LayoutAndPaint,
        affectsStyle: true);

    [MarkupAttribute("background", MarkupValueKind.Color)]
    public static readonly ElementAttribute<Color?> BackgroundAttribute = new(
        "background",
        MarkupValueKind.Color,
        static (element, value) => ((Control)element).Background = value,
        static element => ((Control)element).Background = null,
        InvalidationKind.Paint);

    [MarkupAttribute("input", MarkupValueKind.Input)]
    public static readonly ElementAttribute<bool> InputAttribute = new(
        "input",
        MarkupValueKind.Input,
        static (element, value) => ((Control)element).IsHitTestVisible = value,
        static element => ((Control)element).IsHitTestVisible = true);

    [MarkupAttribute("z-index", MarkupValueKind.Integer)]
    public static readonly ElementAttribute<int> ZIndexAttribute = new(
        "z-index",
        MarkupValueKind.Integer,
        static (element, value) => ((Control)element).ZIndex = value,
        static element => ((Control)element).ZIndex = 0);

    [MarkupAttribute("clip", MarkupValueKind.Boolean)]
    public static readonly ElementAttribute<bool> ClipAttribute = new(
        "clip",
        MarkupValueKind.Boolean,
        static (element, value) => ((Control)element).ClipToBounds = value,
        static element => ((Control)element).ClipToBounds = false,
        InvalidationKind.Paint);

    [MarkupAttribute("focusable", MarkupValueKind.Boolean)]
    public static readonly ElementAttribute<bool> FocusableAttribute = new(
        "focusable",
        MarkupValueKind.Boolean,
        static (element, value) => ((Control)element).IsFocusable = value,
        static element => ((Control)element).IsFocusable = false,
        InvalidationKind.Paint);

    [MarkupAttribute("tab-stop", MarkupValueKind.Boolean)]
    public static readonly ElementAttribute<bool> TabStopAttribute = new(
        "tab-stop",
        MarkupValueKind.Boolean,
        static (element, value) => ((Control)element).IsTabStop = value,
        static element => ((Control)element).IsTabStop = true,
        InvalidationKind.Paint);

    [MarkupAttribute("visible", MarkupValueKind.Boolean)]
    public static readonly ElementAttribute<bool> VisibleAttribute = new(
        "visible",
        MarkupValueKind.Boolean,
        static (element, value) => ((Control)element).IsVisible = value,
        static element => ((Control)element).IsVisible = true);

    [MarkupAttribute("a11y-label")]
    public static readonly ElementAttribute<string?> AccessibilityLabelAttribute = new(
        "a11y-label",
        MarkupValueKind.Any,
        static (element, value) => ((Control)element).Accessibility.Name = value,
        static element => ((Control)element).Accessibility.Name = null,
        InvalidationKind.Paint);

    [MarkupAttribute("a11y-description")]
    public static readonly ElementAttribute<string?> AccessibilityDescriptionAttribute = new(
        "a11y-description",
        MarkupValueKind.Any,
        static (element, value) => ((Control)element).Accessibility.Description = value,
        static element => ((Control)element).Accessibility.Description = null,
        InvalidationKind.Paint);

    [MarkupAttribute("a11y-hidden", MarkupValueKind.Boolean)]
    public static readonly ElementAttribute<bool> AccessibilityHiddenAttribute = new(
        "a11y-hidden",
        MarkupValueKind.Boolean,
        static (element, value) => ((Control)element).Accessibility.IsHidden = value,
        static element => ((Control)element).Accessibility.IsHidden = false,
        InvalidationKind.Paint);

    [MarkupAttribute("disabled", MarkupValueKind.Boolean)]
    public static readonly ElementAttribute<bool> DisabledAttribute = new(
        "disabled",
        MarkupValueKind.Boolean,
        static (element, value) => ((Control)element).SetEnabled(!value),
        static element => ((Control)element).SetEnabled(true),
        InvalidationKind.Paint);

    [MarkupAttribute("anchor", MarkupValueKind.Enum)]
    public static readonly ElementAttribute<Anchor> AnchorAttribute = new(
        "anchor",
        MarkupValueKind.Enum,
        static (element, value) => ((Control)element).Anchor = value,
        static element => ((Control)element).Anchor = Anchor.TopLeft);

    protected Control(string type)
        : base(type) {
    }

    public string? Id { get; private set; }

    public Color? Background { get; private set; }

    public bool IsHitTestVisible { get; private set; } = true;

    public int ZIndex { get; private set; }

    public bool ClipToBounds { get; protected set; }

    public override bool IsFocusable { get; protected set; }

    public bool IsTabStop { get; set; } = true;

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

    public Anchor Anchor { get; private set; } = Anchor.TopLeft;

    protected override ElementAttribute? FindAttribute(string name) {
        return name switch {
            "id" => IdAttribute,
            "background" => BackgroundAttribute,
            "input" => InputAttribute,
            "z-index" => ZIndexAttribute,
            "clip" => ClipAttribute,
            "focusable" => FocusableAttribute,
            "tab-stop" => TabStopAttribute,
            "visible" => VisibleAttribute,
            "a11y-label" => AccessibilityLabelAttribute,
            "a11y-description" => AccessibilityDescriptionAttribute,
            "a11y-hidden" => AccessibilityHiddenAttribute,
            "disabled" => DisabledAttribute,
            "anchor" => AnchorAttribute,
            _ => base.FindAttribute(name),
        };
    }

    public void SetVisible(bool visible) {
        IsVisible = visible;
    }

    public void SetEnabled(bool enabled) {
        if (IsEnabled == enabled) {
            return;
        }

        IsEnabled = enabled;
        OnEnabledChanged(enabled);
        if (!enabled) {
            SetState(UiStateNames.Pressed, false);
        }

        InvalidateState();
    }

    protected internal virtual void OnEnabledChanged(bool enabled) {
    }
}
