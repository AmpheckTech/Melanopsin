using Melanopsin.UI.Input;

namespace Melanopsin.UI.Controls;

public sealed partial class Slider {
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

    private void SetFromPosition(float x) {
        if (_trackWidth <= 0) {
            return;
        }

        var progress = Math.Clamp((x - _trackLeft) / _trackWidth, 0, 1);
        SetValue(Minimum + ((Maximum - Minimum) * progress));
    }
}
