using Melanopsin.UI.Input;

namespace Melanopsin.UI;

public sealed partial class ScrollViewport {
    private bool _pointerInScrollbar;
    private bool _thumbDragging;
    private PointerId _thumbPointer;
    private ScrollAxis _thumbAxis;
    private float _thumbDragStartPosition;
    private float _thumbDragStartOffset;
    private float _thumbDragTravel;
    private float _thumbDragMaximumOffset;

    private void UpdateThumbPointerState(RoutedInputEvent inputEvent) {
        if (inputEvent.Input.Device != InputDeviceKind.Mouse) {
            return;
        }

        var point = new Point(
            inputEvent.CurrentTargetLayoutX,
            inputEvent.CurrentTargetLayoutY);
        _pointerPosition = point;
        _hasPointerPosition = inputEvent.Input.Phase != PointerPhase.Leave;
        var inScrollbar = IsInScrollbarHitArea(point);
        if (inScrollbar == _pointerInScrollbar) {
            return;
        }

        _pointerInScrollbar = inScrollbar;
        InvalidatePaint();
    }

    private bool HandleThumbInput(RoutedInputEvent inputEvent) {
        var phase = inputEvent.Input.Phase;
        if (_thumbDragging) {
            if (inputEvent.Input.PointerId != _thumbPointer) {
                return false;
            }

            if (phase == PointerPhase.Move) {
                DragThumb(inputEvent);
                inputEvent.Handled = true;
                return true;
            }

            if (phase is PointerPhase.Up or PointerPhase.Cancel) {
                if (!inputEvent.Input.IsPrimaryButton) {
                    return false;
                }

                EndThumbDrag();
                inputEvent.Handled = true;
                return true;
            }
        }

        if (phase != PointerPhase.Down ||
            inputEvent.Input.Button is not (MouseButtonKind.None or MouseButtonKind.Left)) {
            return false;
        }

        var point = new Point(
            inputEvent.CurrentTargetLayoutX,
            inputEvent.CurrentTargetLayoutY);
        if (TryBeginThumbDrag(inputEvent, point, ScrollAxis.Vertical) ||
            TryBeginThumbDrag(inputEvent, point, ScrollAxis.Horizontal)) {
            return true;
        }

        if (TryPageFromTrack(point, ScrollAxis.Vertical) ||
            TryPageFromTrack(point, ScrollAxis.Horizontal)) {
            inputEvent.CancelPendingInteraction();
            inputEvent.Handled = true;
            return true;
        }

        return false;
    }

    private bool TryBeginThumbDrag(
        RoutedInputEvent inputEvent,
        Point point,
        ScrollAxis axis) {
        if (!TryGetThumbBar(axis, out var bar, out var maximumOffset) ||
            !bar.Thumb.Contains(point) ||
            !IsInScrollbarHitArea(point, axis)) {
            return false;
        }

        inputEvent.CancelPendingInteraction();
        _thumbDragging = true;
        _thumbPointer = inputEvent.Input.PointerId;
        _thumbAxis = axis;
        _thumbDragStartPosition = axis == ScrollAxis.Vertical ? point.Y : point.X;
        _thumbDragStartOffset = axis == ScrollAxis.Vertical ? OffsetY : OffsetX;
        _thumbDragTravel = Math.Max(0, bar.TrackLength - bar.ThumbLength);
        _thumbDragMaximumOffset = maximumOffset;
        SetState(UiStateNames.Pressed, true);
        inputEvent.Handled = true;
        InvalidatePaint();
        return true;
    }

    private void DragThumb(RoutedInputEvent inputEvent) {
        var position = _thumbAxis == ScrollAxis.Vertical
            ? inputEvent.CurrentTargetLayoutY
            : inputEvent.CurrentTargetLayoutX;
        var progress = _thumbDragTravel <= 0
            ? 0
            : (position - _thumbDragStartPosition) / _thumbDragTravel;
        var offset = Math.Clamp(
            _thumbDragStartOffset + (progress * _thumbDragMaximumOffset),
            0,
            _thumbDragMaximumOffset);
        var changed = _thumbAxis == ScrollAxis.Vertical
            ? _scroll.ScrollTo(OffsetX, offset)
            : _scroll.ScrollTo(offset, OffsetY);
        if (changed) {
            InvalidatePaint();
        }
    }

    private void EndThumbDrag() {
        _thumbDragging = false;
        SetState(UiStateNames.Pressed, false);
        InvalidatePaint();
    }

    private bool TryPageFromTrack(Point point, ScrollAxis axis) {
        if (!TryGetThumbBar(axis, out var bar, out _) ||
            !TryGetTrackHitArea(axis, out var track) ||
            !track.Contains(point) ||
            !IsInScrollbarHitArea(point, axis)) {
            return false;
        }

        var before = axis == ScrollAxis.Vertical
            ? point.Y < bar.Thumb.Y
            : point.X < bar.Thumb.X;
        var page = axis == ScrollAxis.Vertical
            ? _scroll.ViewportHeight
            : _scroll.ViewportWidth;
        var delta = before ? -page : page;
        var changed = axis == ScrollAxis.Vertical
            ? _scroll.ScrollBy(0, delta)
            : _scroll.ScrollBy(delta, 0);
        if (changed) {
            InvalidatePaint();
        }

        return true;
    }
}
