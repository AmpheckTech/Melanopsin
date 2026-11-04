using Melanopsin.UI.Animation;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class CheckBox :
    Control,
    IInputHandler,
    IAnimationParticipant,
    IRenderCommandProvider,
    IAccessibilityActionProvider {
    private static readonly Color UncheckedMarkerColor = new(255, 255, 255, 235);
    private static readonly Color UncheckedBorderColor = new(105, 125, 118);
    private string _text = string.Empty;
    private Color _accentColor = new(32, 112, 90);
    private bool _isChecked;
    private readonly Tween<float> _checkProgress = new(
        0,
        0,
        TimeSpan.FromMilliseconds(150),
        static (from, to, progress) => from + ((to - from) * progress),
        Easing.EaseOut);

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
            AnimateCheck(value);
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

        var input = inputEvent.Input;
        if (!input.IsPrimaryButton) {
            return;
        }

        var phase = input.Phase;
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
        var progress = Math.Clamp(_checkProgress.Value, 0, 1);
        var marker = context.FontSize;
        var markerX = context.Bounds.Right - context.Padding - marker;
        var markerY = context.Bounds.Y + ((context.Bounds.Height - marker) / 2);
        commands.Add(new FillRectangleCommand(
            new Rect(markerX, markerY, marker, marker),
            LerpColor(UncheckedMarkerColor, AccentColor, progress),
            context.FontSize * 0.2f));
        commands.Add(new StrokeRectangleCommand(
            new Rect(markerX, markerY, marker, marker),
            LerpColor(UncheckedBorderColor, AccentColor, progress),
            Math.Max(1, context.FontSize / 14),
            context.FontSize * 0.2f));

        AppendLabel(commands, context, marker);
    }

    bool IAnimationParticipant.AdvanceAnimation(float elapsedSeconds) {
        if (!_checkProgress.Advance(elapsedSeconds)) {
            return false;
        }

        InvalidatePaint();
        return _checkProgress.IsPlaying;
    }

    protected internal override void OnUnmounted() {
        _checkProgress.Pause();
        base.OnUnmounted();
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

    private void AnimateCheck(bool isChecked) {
        _checkProgress.Retarget(isChecked ? 1f : 0f);
        if (!IsMounted) {
            _checkProgress.Seek(1);
        }

        InvalidatePaint();
    }

    private static Color LerpColor(Color from, Color to, float progress) {
        return new Color(
            Lerp(from.Red, to.Red, progress),
            Lerp(from.Green, to.Green, progress),
            Lerp(from.Blue, to.Blue, progress),
            Lerp(from.Alpha, to.Alpha, progress));
    }

    private static byte Lerp(byte from, byte to, float progress) {
        return (byte)Math.Clamp(
            MathF.Round(from + ((to - from) * progress)),
            0,
            255);
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
