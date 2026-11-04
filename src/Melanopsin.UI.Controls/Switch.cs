using Melanopsin.UI.Animation;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Switch :
    Control,
    IInputHandler,
    IAnimationParticipant,
    IRenderCommandProvider,
    IAccessibilityActionProvider {
    private static readonly Color OffTrackColor = new(196, 207, 202);
    private string _text = string.Empty;
    private Color _accentColor = new(32, 112, 90);
    private bool _isOn;
    private readonly Tween<float> _toggleProgress = new(
        0,
        0,
        TimeSpan.FromMilliseconds(180),
        static (from, to, progress) => from + ((to - from) * progress),
        Easing.EaseOut);

    public Switch()
        : base("Switch") {
        IsRepaintBoundary = true;
        InputHandler = this;
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.Switch;
    }

    public event Action<bool>? Toggled;

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

    [MarkupAttribute("on", MarkupValueKind.Boolean)]
    public bool IsOn {
        get => _isOn;
        private set {
            if (_isOn == value) {
                return;
            }

            _isOn = value;
            SetState(UiStateNames.Checked, value);
            AnimateToggle(value);
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
        IsOn = !IsOn;
        Accessibility.Value = IsOn ? "on" : "off";
        Toggled?.Invoke(IsOn);
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
        var progress = Math.Clamp(_toggleProgress.Value, 0, 1);
        var trackWidth = context.FontSize * 2;
        var trackHeight = context.FontSize;
        var trackX = context.Bounds.Right - context.Padding - trackWidth;
        var trackY = context.Bounds.Y + ((context.Bounds.Height - trackHeight) / 2);
        commands.Add(new FillRectangleCommand(
            new Rect(trackX, trackY, trackWidth, trackHeight),
            LerpColor(OffTrackColor, AccentColor, progress),
            trackHeight / 2));
        var knob = trackHeight * 0.78f;
        var knobStart = trackX + ((trackHeight - knob) / 2);
        var knobEnd = trackX + trackWidth - knob - ((trackHeight - knob) / 2);
        var knobX = knobStart + ((knobEnd - knobStart) * progress);
        commands.Add(new FillRectangleCommand(
            new Rect(
                knobX,
                trackY + ((trackHeight - knob) / 2),
                knob,
                knob),
            new Color(255, 255, 255),
            knob / 2));

        if (Text.Length > 0) {
            context.AppendText(
                commands,
                Text,
                new Rect(
                    context.Bounds.X + context.Padding,
                    context.Bounds.Y,
                    Math.Max(0, context.Bounds.Width - (context.Padding * 2) - trackWidth - 8),
                    context.Bounds.Height),
                context.Foreground,
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center,
                wrap: false);
        }
    }

    bool IAnimationParticipant.AdvanceAnimation(float elapsedSeconds) {
        if (!_toggleProgress.Advance(elapsedSeconds)) {
            return false;
        }

        InvalidatePaint();
        return _toggleProgress.IsPlaying;
    }

    protected internal override void OnUnmounted() {
        _toggleProgress.Pause();
        base.OnUnmounted();
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "text":
                Text = value?.ToString() ?? string.Empty;
                break;
            case "on":
                IsOn = value is bool isOn && isOn;
                Accessibility.Value = IsOn ? "on" : "off";
                break;
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name == "on"
            ? InvalidationKind.Paint
            : base.ClassifyAttributeChange(name);
    }

    private void AnimateToggle(bool isOn) {
        var target = isOn ? 1f : 0f;
        _toggleProgress.Retarget(target);
        if (!IsMounted) {
            _toggleProgress.Seek(1);
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
}
