using Melanopsin.UI.Input;

namespace Melanopsin.UI.Controls;

public sealed partial class VirtualList {
    private const float DragThreshold = 6;
    private PointerId? _dragPointerId;
    private float _dragStartY;
    private float _dragStartOffset;
    private int _pressedRow = -1;
    private bool _dragging;

    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            ResetPointerInteraction();
            return;
        }

        var input = inputEvent.Input;
        if (input.Phase == PointerPhase.Scroll && input.WheelDelta != 0) {
            ScrollBy(-input.WheelDelta / 120f * 48);
            inputEvent.Handled = true;
            return;
        }

        if (!input.IsPrimaryButton || _viewportHeight <= 0) {
            return;
        }

        var phase = input.Phase;
        if (phase == PointerPhase.Down &&
            _dragPointerId is null &&
            _layoutBounds.Contains(input.X, input.Y)) {
            EnsureRowOffsets();
            _dragPointerId = input.PointerId;
            _dragStartY = input.Y;
            _dragStartOffset = _scrollOffset;
            _pressedRow = FindRowAt(input.Y);
            _dragging = false;
            inputEvent.Handled = true;
            return;
        }

        if (_dragPointerId != input.PointerId) {
            return;
        }

        if (phase == PointerPhase.Move) {
            var deltaY = input.Y - _dragStartY;
            if (!_dragging && Math.Abs(deltaY) >= DragThreshold) {
                _dragging = true;
                _pressedRow = -1;
            }

            if (_dragging) {
                ScrollBy(_dragStartOffset - deltaY);
                inputEvent.Handled = true;
            }

            return;
        }

        if (phase == PointerPhase.Up) {
            var pressedRow = _pressedRow;
            var wasDragging = _dragging;
            var selectedRow = _layoutBounds.Contains(input.X, input.Y)
                ? FindRowAt(input.Y)
                : -1;
            ResetPointerInteraction();
            if (!wasDragging && selectedRow >= 0 && selectedRow == pressedRow) {
                Select(selectedRow);
            }

            inputEvent.Handled = true;
        } else if (phase is PointerPhase.Cancel or PointerPhase.Leave) {
            ResetPointerInteraction();
        }
    }

    private int FindRowAt(float layoutY) {
        var localY = layoutY - _layoutBounds.Y + _scrollOffset;
        var row = _rowLayout.FindRowAt(localY, ItemCount);
        return row >= 0 && row < ItemCount ? row : -1;
    }

    private void ResetPointerInteraction() {
        _dragPointerId = null;
        _dragStartY = 0;
        _dragStartOffset = 0;
        _pressedRow = -1;
        _dragging = false;
    }
}
