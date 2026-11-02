using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class RadioButton :
    Control,
    IInputHandler,
    IRenderCommandProvider,
    IAccessibilityActionProvider {
    private string _text = string.Empty;
    private Color _accentColor = new(32, 112, 90);
    private bool _isSelected;

    public RadioButton()
        : base("RadioButton") {
        IsRepaintBoundary = true;
        InputHandler = this;
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.RadioButton;
    }

    public event Action? Selected;

    public AccessibilityAction SupportedActions => IsEnabled
        ? AccessibilityAction.Default | AccessibilityAction.Select
        : AccessibilityAction.None;

    public bool PerformAccessibilityAction(AccessibilityAction action) {
        if (!IsEnabled ||
            action is not (AccessibilityAction.Default or AccessibilityAction.Select)) {
            return false;
        }

        Select();
        return true;
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

    [MarkupAttribute("group")]
    public string Group { get; private set; } = "default";

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

    public Color AccentColor {
        get => _accentColor;
        set {
            if (_accentColor == value) {
                return;
            }

            _accentColor = value;
            InvalidatePaint();
        }
    }

    public void Select() {
        if (IsSelected) {
            return;
        }

        IsSelected = true;
        Accessibility.Value = "selected";
        DeselectSiblings();
        Selected?.Invoke();
    }

    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            SetState(UiStateNames.Pressed, false);
            return;
        }

        var phase = inputEvent.Input.Phase;
        if (phase == PointerPhase.Down) {
            SetState(UiStateNames.Pressed, true);
        } else if (phase == PointerPhase.Up) {
            var select = HasState(UiStateNames.Pressed) && inputEvent.IsInsideTarget;
            SetState(UiStateNames.Pressed, false);
            if (select) {
                Select();
            }
        } else if (phase is PointerPhase.Cancel or PointerPhase.Leave) {
            SetState(UiStateNames.Pressed, false);
        }
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        var marker = context.FontSize;
        var markerX = context.Bounds.Right - context.Padding - marker;
        var markerY = context.Bounds.Y + ((context.Bounds.Height - marker) / 2);
        commands.Add(new FillRectangleCommand(
            new Rect(markerX, markerY, marker, marker),
            new Color(255, 255, 255, 235),
            marker / 2));
        commands.Add(new StrokeRectangleCommand(
            new Rect(markerX, markerY, marker, marker),
            IsSelected ? AccentColor : new Color(105, 125, 118),
            Math.Max(1, context.FontSize / 14),
            marker / 2));
        if (IsSelected) {
            var dot = marker * 0.42f;
            commands.Add(new FillRectangleCommand(
                new Rect(
                    markerX + ((marker - dot) / 2),
                    markerY + ((marker - dot) / 2),
                    dot,
                    dot),
                AccentColor,
                dot / 2));
        }

        if (Text.Length > 0) {
            context.AppendText(
                commands,
                Text,
                new Rect(
                    context.Bounds.X + context.Padding,
                    context.Bounds.Y,
                    Math.Max(0, context.Bounds.Width - (context.Padding * 2) - marker - 8),
                    context.Bounds.Height),
                context.Foreground,
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center,
                wrap: false);
        }
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "text":
                Text = value?.ToString() ?? string.Empty;
                break;
            case "group":
                Group = value?.ToString() ?? "default";
                break;
            case "selected" when value is true:
                IsSelected = true;
                Accessibility.Value = "selected";
                break;
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name == "selected"
            ? InvalidationKind.Paint
            : base.ClassifyAttributeChange(name);
    }

    private void DeselectSiblings() {
        var root = Parent;
        while (root?.Parent is not null) {
            root = root.Parent;
        }

        if (root is not null) {
            Deselect(root);
        }
    }

    private void Deselect(Node node) {
        if (node is RadioButton radio &&
            !ReferenceEquals(radio, this) &&
            string.Equals(radio.Group, Group, StringComparison.Ordinal)) {
            radio.IsSelected = false;
            radio.Accessibility.Value = null;
        }

        if (node is Element element) {
            foreach (var child in element.Children) {
                Deselect(child);
            }
        }
    }
}
