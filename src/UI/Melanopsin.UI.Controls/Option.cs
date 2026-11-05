// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

/// <summary>
/// A selectable option owned by a ComboBox or option-based list control.
/// </summary>
[MarkupElement]
public sealed class Option :
    Control,
    ISingleLineTextContent,
    IRenderCommandProvider {
    private string _text = string.Empty;
    private bool _isSelected;

    public Option()
        : base("Option") {
        Accessibility.Role = AccessibilityRole.ListItem;
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

    [MarkupAttribute("value")]
    public string? Value { get; private set; }

    [MarkupAttribute("selected", MarkupValueKind.Boolean)]
    public bool IsSelected {
        get => _isSelected;
        private set {
            if (_isSelected == value) {
                return;
            }

            _isSelected = value;
            SetState(UiStateNames.Selected, value);
        }
    }

    public void AppendText(string value) {
        Text += value;
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (Parent is ComboBox) {
            return;
        }

        if (IsSelected) {
            commands.Add(new FillRectangleCommand(
                context.Bounds,
                new Color(226, 238, 232),
                context.CornerRadius));
        }

        context.AppendText(
            commands,
            Text,
            new Rect(
                context.Bounds.MinX + context.Padding,
                context.Bounds.MinY,
                Math.Max(0, context.Bounds.Width - (context.Padding * 2)),
                context.Bounds.Height),
            context.Foreground,
            TextHorizontalAlignment.Start,
            TextVerticalAlignment.Center,
            wrap: false);
    }

    internal void SetSelected(bool selected) {
        if (IsSelected == selected) {
            return;
        }

        IsSelected = selected;
        Accessibility.Value = selected ? "selected" : "unselected";
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "text":
                Text = value?.ToString() ?? string.Empty;
                break;
            case "value":
                Value = value?.ToString();
                break;
            case "selected":
                IsSelected = value is bool isSelected && isSelected;
                Accessibility.Value = IsSelected ? "selected" : "unselected";
                break;
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name == "selected"
            ? InvalidationKind.Paint
            : base.ClassifyAttributeChange(name);
    }
}
