namespace Melanopsin.UI.Controls;

public sealed partial class Slider {
    public void SetValue(double value) {
        SetCore(value);
    }

    private void SetCore(double value) {
        if (!double.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The slider value must be finite.");
        }

        var next = Quantize(Math.Clamp(value, Minimum, Maximum));
        Accessibility.Value = FormatValue(next);
        if (Math.Abs(_value - next) < 0.0001) {
            return;
        }

        _value = next;
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

    private static string FormatValue(double value) {
        return value.ToString(
            "0.##",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
