using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Slider :
    Control,
    IInputHandler,
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

    public void SetValue(double value) {
        SetCore(value);
    }

    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            _dragging = false;
            return;
        }

        var phase = inputEvent.Input.Phase;
        if (phase == PointerPhase.Down) {
            _dragging = true;
            SetFromPosition(inputEvent.LayoutX);
        } else if (phase == PointerPhase.Move && _dragging) {
            SetFromPosition(inputEvent.LayoutX);
        } else if (phase is PointerPhase.Up or PointerPhase.Cancel) {
            _dragging = false;
        }
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        _trackLeft = context.Bounds.X + context.Padding;
        _trackWidth = Math.Max(1, context.Bounds.Width - (context.Padding * 2));
        var trackHeight = Math.Max(2, context.FontSize * 0.22f);
        var trackY = context.Bounds.Y + ((context.Bounds.Height - trackHeight) / 2);
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
        var knobX = _trackLeft + (_trackWidth * progress) - (knob / 2);
        commands.Add(new FillRectangleCommand(
            new Rect(
                Math.Clamp(knobX, _trackLeft - (knob / 2), _trackLeft + _trackWidth - (knob / 2)),
                context.Bounds.Y + ((context.Bounds.Height - knob) / 2),
                knob,
                knob),
            AccentColor,
            knob / 2));
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "value":
                SetValue(ControlValueParser.ReadNumber(value) ?? Value);
                break;
            case "minimum":
                SetMinimum(ControlValueParser.ReadNumber(value) ?? Minimum);
                break;
            case "maximum":
                SetMaximum(ControlValueParser.ReadNumber(value) ?? Maximum);
                break;
            case "step":
                SetStep(ControlValueParser.ReadNumber(value) ?? Step);
                break;
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name is "value" or "minimum" or "maximum" or "step"
            ? InvalidationKind.Paint
            : base.ClassifyAttributeChange(name);
    }

    private void SetFromPosition(float x) {
        if (_trackWidth <= 0) {
            return;
        }

        var progress = Math.Clamp((x - _trackLeft) / _trackWidth, 0, 1);
        SetValue(Minimum + ((Maximum - Minimum) * progress));
    }

    private void SetCore(double value) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The slider value must be finite.");
        }

        var next = Quantize(Math.Clamp(value, Minimum, Maximum));
        if (Math.Abs(_value - next) < 0.0001) {
            return;
        }

        _value = next;
        Accessibility.Value = next.ToString(
            "0.##",
            System.Globalization.CultureInfo.InvariantCulture);
        InvalidatePaint();
        ValueChanged?.Invoke(next);
    }

    private void SetMinimum(double value) {
        ValidateBound(value, nameof(Minimum));
        if (_maximumConfigured && value > _maximum) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Minimum cannot be greater than Maximum.");
        }

        if (Math.Abs(_minimum - value) < 0.0001 && _minimumConfigured) {
            return;
        }

        _minimum = value;
        _minimumConfigured = true;
        if (_maximum < _minimum) {
            _maximum = _minimum;
        }

        SetCore(_value);
    }

    private void SetMaximum(double value) {
        ValidateBound(value, nameof(Maximum));
        if (_minimumConfigured && value < _minimum) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Maximum cannot be smaller than Minimum.");
        }

        if (Math.Abs(_maximum - value) < 0.0001 && _maximumConfigured) {
            return;
        }

        _maximum = value;
        _maximumConfigured = true;
        if (_minimum > _maximum) {
            _minimum = _maximum;
        }

        SetCore(_value);
    }

    private void SetStep(double value) {
        if (!double.IsFinite(value) || value <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Step must be a positive finite number.");
        }

        if (Math.Abs(Step - value) < 0.0000001) {
            return;
        }

        Step = value;
        SetCore(_value);
    }

    private double Quantize(double value) {
        if (Step <= 0 || (Maximum - Minimum) <= 0) {
            return value;
        }

        // The endpoints are legal values even when the range is not an exact
        // multiple of Step. Without this, a value such as 100 in the range
        // 0..100 with Step 30 would snap back to 90 forever.
        if (value >= Maximum) {
            return Maximum;
        }

        if (value <= Minimum) {
            return Minimum;
        }

        var stepIndex = Math.Floor((value - Minimum) / Step);
        var lower = Math.Min(Maximum, Minimum + (stepIndex * Step));
        var upper = Math.Min(Maximum, Minimum + ((stepIndex + 1) * Step));
        if (!double.IsFinite(lower)) {
            return Minimum;
        }

        if (!double.IsFinite(upper) || upper >= Maximum) {
            upper = Maximum;
        }

        var lowerDistance = value - lower;
        var upperDistance = upper - value;
        return lowerDistance < upperDistance ? lower : upper;
    }

    private double GetAdjacentValue(int direction) {
        if ((Maximum - Minimum) <= 0) {
            return Minimum;
        }

        if (direction > 0) {
            if (Value >= Maximum) {
                return Maximum;
            }

            var (isGridValue, stepIndex) = GetGridPosition(Value);
            if (!isGridValue) {
                return Maximum;
            }

            var next = Minimum + ((stepIndex + 1) * Step);
            return !double.IsFinite(next) || next >= Maximum
                ? Maximum
                : next;
        }

        if (Value <= Minimum) {
            return Minimum;
        }

        var (isDecrementGridValue, decrementIndex) = GetGridPosition(Value);
        if (!isDecrementGridValue) {
            var lower = Math.Min(
                Maximum,
                Minimum + (Math.Floor((Value - Minimum) / Step) * Step));
            return double.IsFinite(lower) ? Math.Max(Minimum, lower) : Minimum;
        }

        if (decrementIndex <= 0) {
            return Minimum;
        }

        var previous = Minimum + ((decrementIndex - 1) * Step);
        return double.IsFinite(previous)
            ? Math.Max(Minimum, previous)
            : Minimum;
    }

    private bool SetAdjacentValue(int direction) {
        var next = GetAdjacentValue(direction);
        if (Math.Abs(Value - next) < 0.0001) {
            return false;
        }

        SetValue(next);
        return true;
    }

    private (bool IsGridValue, double StepIndex) GetGridPosition(double value) {
        var rawIndex = (value - Minimum) / Step;
        var roundedIndex = Math.Round(rawIndex);
        var snapped = Minimum + (roundedIndex * Step);
        var tolerance = Math.Max(1e-9, Math.Abs(Step) * 1e-9);
        return Math.Abs(value - snapped) <= tolerance
            ? (true, roundedIndex)
            : (false, Math.Floor(rawIndex));
    }

    private static void ValidateBound(double value, string parameterName) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Slider bounds must be finite.");
        }
    }
}
