using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class CheckBox :
    Control,
    IInputHandler,
    IRenderCommandProvider,
    IAccessibilityActionProvider {
    private string _text = string.Empty;
    private Color _accentColor = new(32, 112, 90);
    private bool _isChecked;

    public CheckBox()
        : base("CheckBox") {
        IsRepaintBoundary = true;
        InputHandler = this;
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.CheckBox;
    }

    public event Action<bool>? CheckedChanged;

    public AccessibilityAction SupportedActions => IsEnabled
        ? AccessibilityAction.Default | AccessibilityAction.Toggle
        : AccessibilityAction.None;

    public bool PerformAccessibilityAction(AccessibilityAction action) {
        if (!IsEnabled ||
            action is not (AccessibilityAction.Default or AccessibilityAction.Toggle)) {
            return false;
        }

        Toggle();
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

    [MarkupAttribute("checked", MarkupValueKind.Boolean)]
    public bool IsChecked {
        get => _isChecked;
        private set {
            if (_isChecked == value) {
                return;
            }

            _isChecked = value;
            SetState(UiStateNames.Checked, value);
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

    public void Toggle() {
        IsChecked = !IsChecked;
        Accessibility.Value = IsChecked ? "checked" : "unchecked";
        CheckedChanged?.Invoke(IsChecked);
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
            var toggle = HasState(UiStateNames.Pressed) && inputEvent.IsInsideTarget;
            SetState(UiStateNames.Pressed, false);
            if (toggle) {
                Toggle();
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
            IsChecked ? AccentColor : new Color(255, 255, 255, 235),
            context.FontSize * 0.2f));
        commands.Add(new StrokeRectangleCommand(
            new Rect(markerX, markerY, marker, marker),
            IsChecked ? AccentColor : new Color(105, 125, 118),
            Math.Max(1, context.FontSize / 14),
            context.FontSize * 0.2f));

        AppendLabel(commands, context, marker);
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "text":
                Text = value as string ?? string.Empty;
                break;
            case "checked":
                IsChecked = value is bool isChecked && isChecked;
                Accessibility.Value = IsChecked ? "checked" : "unchecked";
                break;
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name == "checked"
            ? InvalidationKind.Paint
            : base.ClassifyAttributeChange(name);
    }

    private void AppendLabel(
        List<RenderCommand> commands,
        RenderCommandContext context,
        float marker) {
        if (Text.Length == 0) {
            return;
        }

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
