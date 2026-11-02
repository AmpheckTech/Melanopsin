using Melanopsin.UI.Input;

namespace Melanopsin.UI.Controls;

public sealed partial class ComboBox {
    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            SetState(UiStateNames.Pressed, false);
            return;
        }

        var phase = inputEvent.Input.Phase;
        if (phase == PointerPhase.Down) {
            SetState(UiStateNames.Pressed, true);
        } else if (phase == PointerPhase.Up) {
            var activate = HasState(UiStateNames.Pressed) && inputEvent.IsInsideTarget;
            SetState(UiStateNames.Pressed, false);
            if (activate) {
                Activate(inputEvent);
            }
        } else if (phase is PointerPhase.Cancel or PointerPhase.Leave) {
            SetState(UiStateNames.Pressed, false);
        }
    }

    public bool HandleKey(KeyInputEvent input) {
        if (input.Phase != KeyPhase.Down || !IsEnabled) {
            return false;
        }

        var current = SelectedIndex < 0 ? 0 : SelectedIndex;
        switch (input.Key) {
            case Key.Escape when IsOpen:
                SetExpanded(false);
                return true;
            case Key.Enter:
            case Key.Space:
                SetExpanded(!IsOpen);
                return true;
            case Key.Down when IsOpen:
                Select(Math.Min(_options.Count - 1, current + 1));
                return _options.Count > 0;
            case Key.Up when IsOpen:
                Select(Math.Max(0, current - 1));
                return _options.Count > 0;
            case Key.Down when !IsOpen:
                SetExpanded(true);
                return true;
            case Key.Up when !IsOpen:
                SetExpanded(true);
                return true;
            case Key.Home when IsOpen:
                Select(0);
                return _options.Count > 0;
            case Key.End when IsOpen:
                Select(_options.Count - 1);
                return _options.Count > 0;
            default:
                return false;
        }
    }

    private void Activate(RoutedInputEvent inputEvent) {
        if (!IsOpen) {
            SetExpanded(true);
            return;
        }

        if (inputEvent.Target is Option option) {
            var optionIndex = _options.IndexOf(option);
            SetExpanded(false);
            if (optionIndex >= 0) {
                Select(optionIndex);
            }

            return;
        }

        SetExpanded(false);
    }
}
