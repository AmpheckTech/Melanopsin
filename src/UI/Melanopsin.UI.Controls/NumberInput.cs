using System.Globalization;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed partial class NumberInput :
    LinearLayout,
    IKeyboardInputHandler,
    IAccessibilityActionProvider {
    private readonly TextInput _editor;
    private readonly Button _decrement;
    private readonly Button _increment;
    private double _value;
    private double _minimum;
    private double _maximum = 100;
    private double _step = 1;
    private bool _minimumConfigured;
    private bool _maximumConfigured;

    public NumberInput()
        : base("NumberInput") {
        SetAttributeValue("orientation", "horizontal");
        SetAttributeValue("spacing", "0.5su");
        InlineStyle.Height = Length.Su(8);

        _decrement = CreateStepButton("-", AdjustDown);
        _editor = new TextInput();
        _editor.Purpose = TextInputPurpose.Number;
        _editor.SetAttributeValue("class", "number-input-editor");
        _editor.InlineStyle.Weight = 1;
        _editor.Submitted += _ => CommitEditorText();
        _editor.FocusChanged += focused => {
            if (!focused) {
                CommitEditorText();
            }
        };
        _increment = CreateStepButton("+", AdjustUp);

        ReplaceChildren([_decrement, _editor, _increment]);
        UpdateEditorText();
        UpdateStepButtons();
        Accessibility.Role = AccessibilityRole.SpinButton;
    }

    public event Action<double>? ValueChanged;

    [MarkupAttribute("value", MarkupValueKind.Number)]
    public double Value {
        get => _value;
        set => SetValue(value);
    }

    [MarkupAttribute("minimum", MarkupValueKind.Number)]
    public double Minimum => _minimum;

    [MarkupAttribute("maximum", MarkupValueKind.Number)]
    public double Maximum => _maximum;

    [MarkupAttribute("step", MarkupValueKind.Number)]
    public double Step => _step;

    public AccessibilityAction SupportedActions => IsEnabled
        ? AccessibilityAction.Increment |
          AccessibilityAction.Decrement |
          AccessibilityAction.Default
        : AccessibilityAction.None;

    public void Increment() => SetValue(Value + Step);

    public void Decrement() => SetValue(Value - Step);

    public bool PerformAccessibilityAction(AccessibilityAction action) {
        if (!IsEnabled) {
            return false;
        }

        switch (action) {
            case AccessibilityAction.Increment:
                Increment();
                return true;
            case AccessibilityAction.Decrement:
                Decrement();
                return true;
            case AccessibilityAction.Default:
                CommitEditorText();
                return true;
            default:
                return false;
        }
    }

    public bool HandleKey(KeyInputEvent input) {
        if (input.Phase != KeyPhase.Down || !IsEnabled) {
            return false;
        }

        switch (input.Key) {
            case Key.Down:
                Decrement();
                return true;
            case Key.Up:
                Increment();
                return true;
            case Key.PageDown:
                SetValue(Value - (Step * 10));
                return true;
            case Key.PageUp:
                SetValue(Value + (Step * 10));
                return true;
            default:
                return false;
        }
    }

    public override void AppendChild(Node child) {
        throw new InvalidOperationException(
            "NumberInput owns its editor and step buttons.");
    }

    public override void InsertChild(int index, Node child) {
        throw new InvalidOperationException(
            "NumberInput owns its editor and step buttons.");
    }

    protected internal override bool ReconcilesChildren => false;

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "value":
                if (value is double number) {
                    SetValue(number);
                }

                break;
            case "minimum":
                if (value is double minimum) {
                    SetMinimum(minimum);
                }

                break;
            case "maximum":
                if (value is double maximum) {
                    SetMaximum(maximum);
                }

                break;
            case "step":
                if (value is double step) {
                    SetStep(step);
                }

                break;
            case "placeholder":
                var placeholder = value?.ToString() ?? string.Empty;
                _editor.SetAttributeValue("placeholder", placeholder);
                if (string.IsNullOrEmpty(Accessibility.Name)) {
                    Accessibility.Name = placeholder;
                }

                break;
        }
    }

    protected internal override void OnEnabledChanged(bool enabled) {
        base.OnEnabledChanged(enabled);
        _editor.SetEnabled(enabled);
        if (!enabled) {
            _decrement.SetEnabled(false);
            _increment.SetEnabled(false);
            return;
        }

        UpdateStepButtons();
    }

    private Button CreateStepButton(string text, Action action) {
        var button = new Button();
        button.SetAttributeValue("text", text);
        button.SetAttributeValue("class", "number-input-step");
        button.IsTabStop = false;
        button.InlineStyle.Width = Length.Su(7);
        button.Clicked += action;
        return button;
    }

    private void AdjustDown() => Decrement();

    private void AdjustUp() => Increment();

    private void CommitEditorText() {
        if (!IsEnabled) {
            return;
        }

        if (TryParseEditorText(out var parsed)) {
            SetValue(parsed);
        }

        UpdateEditorText();
    }

    private bool TryParseEditorText(out double parsed) {
        return double.TryParse(
                   _editor.Text,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.CurrentCulture,
                   out parsed) ||
               double.TryParse(
                   _editor.Text,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out parsed);
    }

    private void UpdateEditorText() {
        var text = Value.ToString("0.###", CultureInfo.CurrentCulture);
        if (!string.Equals(_editor.Text, text, StringComparison.Ordinal)) {
            _editor.SetText(text);
        }

        Accessibility.Value = text;
    }

    private void UpdateStepButtons() {
        _decrement.SetEnabled(IsEnabled && Value > Minimum);
        _increment.SetEnabled(IsEnabled && Value < Maximum);
    }
}
