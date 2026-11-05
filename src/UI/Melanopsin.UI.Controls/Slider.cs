// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed partial class Slider :
    Control,
    IInputHandler,
    IKeyboardInputHandler,
    IRenderCommandProvider,
    IAccessibilityActionProvider {
    private bool _dragging;
    private float _trackLeft;
    private float _trackWidth;
    private double _value;
    private double _minimum;
    private double _maximum = 100;
    private bool _minimumConfigured;
    private bool _maximumConfigured;
    private Color _accentColor = new(32, 112, 90);

    public Slider()
        : base("Slider") {
        IsRepaintBoundary = true;
        InputHandler = this;
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.Slider;
        Accessibility.Value = FormatValue(_value);
    }

    public event Action<double>? ValueChanged;

    [MarkupAttribute("value", MarkupValueKind.Number)]
    public double Value {
        get => _value;
        set => SetCore(value);
    }

    [MarkupAttribute("minimum", MarkupValueKind.Number)]
    public double Minimum {
        get => _minimum;
    }

    [MarkupAttribute("maximum", MarkupValueKind.Number)]
    public double Maximum {
        get => _maximum;
    }

    [MarkupAttribute("step", MarkupValueKind.Number)]
    public double Step {
        get;
        private set;
    } = 1;

    public AccessibilityAction SupportedActions => IsEnabled
        ? AccessibilityAction.Increment | AccessibilityAction.Decrement
        : AccessibilityAction.None;

    public bool PerformAccessibilityAction(AccessibilityAction action) {
        if (!IsEnabled) {
            return false;
        }

        switch (action) {
            case AccessibilityAction.Increment:
                return SetAdjacentValue(direction: 1);
            case AccessibilityAction.Decrement:
                return SetAdjacentValue(direction: -1);
            default:
                return false;
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

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        _trackLeft = context.Bounds.MinX + context.Padding;
        _trackWidth = Math.Max(1, context.Bounds.Width - (context.Padding * 2));
        var trackHeight = Math.Max(2, context.FontSize * 0.22f);
        var trackY = context.Bounds.MinY + ((context.Bounds.Height - trackHeight) / 2);
        var range = Maximum - Minimum;
        var progress = range <= 0 ? 0 : (float)((Value - Minimum) / range);
        commands.Add(new FillRectangleCommand(
            new Rect(_trackLeft, trackY, _trackWidth, trackHeight),
            new Color(205, 216, 211),
            trackHeight / 2));
        commands.Add(new FillRectangleCommand(
            new Rect(_trackLeft, trackY, _trackWidth * progress, trackHeight),
            AccentColor,
            trackHeight / 2));
        var knob = context.FontSize;
        var knobX = Math.Clamp(
            _trackLeft + (_trackWidth * progress) - (knob / 2),
            _trackLeft - (knob / 2),
            _trackLeft + _trackWidth - (knob / 2));
        var knobY = context.Bounds.MinY + ((context.Bounds.Height - knob) / 2);
        if (IsFocused) {
            var focusRing = knob * 1.6f;
            commands.Add(new FillRectangleCommand(
                new Rect(
                    knobX - ((focusRing - knob) / 2),
                    knobY - ((focusRing - knob) / 2),
                    focusRing,
                    focusRing),
                new Color(
                    AccentColor.Red,
                    AccentColor.Green,
                    AccentColor.Blue,
                    48),
                focusRing / 2));
        }

        commands.Add(new FillRectangleCommand(
            new Rect(knobX, knobY, knob, knob),
            AccentColor,
            knob / 2));
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "value":
                SetValue(value is float progress ? progress : Value);
                break;
            case "minimum":
                SetMinimum(value is float minimum ? minimum : Minimum);
                break;
            case "maximum":
                SetMaximum(value is float maximum ? maximum : Maximum);
                break;
            case "step":
                SetStep(value is float step ? step : Step);
                break;
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name is "value" or "minimum" or "maximum" or "step"
            ? InvalidationKind.Paint
            : base.ClassifyAttributeChange(name);
    }

}
