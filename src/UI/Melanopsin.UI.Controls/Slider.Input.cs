// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Input;

namespace Melanopsin.UI.Controls;

public sealed partial class Slider {
    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            _dragging = false;
            return;
        }

        var input = inputEvent.Input;
        if (!input.IsPrimaryButton) {
            return;
        }

        var phase = input.Phase;
        if (phase == PointerPhase.Down) {
            _dragging = true;
            SetFromPosition(inputEvent.LayoutX);
            inputEvent.Handled = true;
        } else if (phase == PointerPhase.Move && _dragging) {
            SetFromPosition(inputEvent.LayoutX);
            inputEvent.Handled = true;
        } else if ((phase is PointerPhase.Up or PointerPhase.Cancel) && _dragging) {
            _dragging = false;
            inputEvent.Handled = true;
        }
    }

    public bool HandleKey(KeyInputEvent input) {
        if (input.Phase != KeyPhase.Down || !IsEnabled) {
            return false;
        }

        switch (input.Key) {
            case Key.Left:
            case Key.Down:
                SetAdjacentValue(-1);
                return true;
            case Key.Right:
            case Key.Up:
                SetAdjacentValue(1);
                return true;
            case Key.PageDown:
                SetValue(Value - (Step * 10));
                return true;
            case Key.PageUp:
                SetValue(Value + (Step * 10));
                return true;
            case Key.Home:
                SetValue(Minimum);
                return true;
            case Key.End:
                SetValue(Maximum);
                return true;
            default:
                return false;
        }
    }

    private void SetFromPosition(float x) {
        if (_trackWidth <= 0) {
            return;
        }

        var progress = Math.Clamp((x - _trackLeft) / _trackWidth, 0, 1);
        SetValue(Minimum + ((Maximum - Minimum) * progress));
    }
}
