namespace Melanopsin.UI.Controls;

public sealed partial class NumberInput {
    public void SetValue(double value) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The number value must be finite.");
        }

        var next = Quantize(Math.Clamp(value, Minimum, Maximum));
        if (Math.Abs(_value - next) < 0.0001) {
            UpdateEditorText();
            UpdateStepButtons();
            return;
        }

        _value = next;
        UpdateEditorText();
        UpdateStepButtons();
        ValueChanged?.Invoke(next);
    }

    private void SetMinimum(double value) {
        ValidateBound(value, nameof(Minimum));
        if (_maximumConfigured && value > Maximum) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Minimum cannot be greater than Maximum.");
        }

        _minimum = value;
        _minimumConfigured = true;
        if (Maximum < Minimum) {
            _maximum = Minimum;
        }

        SetValue(_value);
    }

    private void SetMaximum(double value) {
        ValidateBound(value, nameof(Maximum));
        if (_minimumConfigured && value < Minimum) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Maximum cannot be smaller than Minimum.");
        }

        _maximum = value;
        _maximumConfigured = true;
        if (Minimum > Maximum) {
            _minimum = Maximum;
        }

        SetValue(_value);
    }

    private void SetStep(double value) {
        ValidateBound(value, nameof(Step));
        if (value <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Step must be greater than zero.");
        }

        _step = value;
        SetValue(_value);
    }

    private double Quantize(double value) {
        if (value <= Minimum) {
            return Minimum;
        }

        if (value >= Maximum) {
            return Maximum;
        }

        var index = Math.Round((value - Minimum) / Step);
        return Math.Clamp(Minimum + (index * Step), Minimum, Maximum);
    }

    private static void ValidateBound(double value, string parameterName) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "NumberInput bounds must be finite.");
        }
    }
}
