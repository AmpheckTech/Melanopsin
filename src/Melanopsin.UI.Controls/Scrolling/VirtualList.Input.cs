using Melanopsin.UI.Input;

namespace Melanopsin.UI.Controls;

public sealed partial class VirtualList {
    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            return;
        }

        var input = inputEvent.Input;
        if (input.Phase == PointerPhase.Scroll && input.WheelDelta != 0) {
            ScrollBy(-input.WheelDelta / 120f * 48);
            inputEvent.Handled = true;
            return;
        }

        if (input.Phase != PointerPhase.Down || _viewportHeight <= 0) {
            return;
        }

        EnsureRowOffsets();
        var localY = input.Y - _layoutBounds.Y + _scrollOffset;
        var row = _rowLayout.FindRowAt(localY, ItemCount);
        if (row >= 0 && row < ItemCount) {
            Select(row);
            inputEvent.Handled = true;
        }
    }
}
