namespace Melanopsin.UI;

internal sealed class ScrollController {
    private const float OverscrollResistance = 0.35f;
    private const float SpringStiffness = 160f;
    private const float SpringDamping = 24f;
    private const float FlingDamping = 4.8f;
    private const float MaxVelocity = 6000f;
    private const float AnimationStep = 1f / 240f;
    private static readonly SpringConfig ScrollSpring = new(
        SpringStiffness,
        SpringDamping);

    private ScrollAxis _axis = ScrollAxis.Vertical;
    private bool _dragging;
    private float _dragStartX;
    private float _dragStartY;
    private float _dragOffsetX;
    private float _dragOffsetY;
    private float _lastDragX;
    private float _lastDragY;
    private long _lastMoveTimestamp;
    private float _velocityX;
    private float _velocityY;
    private bool _settling;

    public float OffsetX { get; private set; }

    public float OffsetY { get; private set; }

    public float MaxOffsetX { get; private set; }

    public float MaxOffsetY { get; private set; }

    public float ViewportWidth { get; private set; }

    public float ViewportHeight { get; private set; }

    public float ContentWidth { get; private set; }

    public float ContentHeight { get; private set; }

    public float MaxOverscroll { get; set; } = 24;

    public ScrollAxis Axis => _axis;

    public bool IsDragging => _dragging;

    public bool IsHorizontalEnabled =>
        _axis is ScrollAxis.Horizontal or ScrollAxis.Both;

    public bool IsVerticalEnabled =>
        _axis is ScrollAxis.Vertical or ScrollAxis.Both;

    public void SetAxis(ScrollAxis axis) {
        _axis = axis;
    }

    public bool ScrollBy(float deltaX, float deltaY) {
        var previousX = OffsetX;
        var previousY = OffsetY;
        if (IsHorizontalEnabled) {
            OffsetX = Math.Clamp(OffsetX + deltaX, 0, MaxOffsetX);
        }

        if (IsVerticalEnabled) {
            OffsetY = Math.Clamp(OffsetY + deltaY, 0, MaxOffsetY);
        }

        return OffsetX != previousX || OffsetY != previousY;
    }

    public bool ScrollTo(float offsetX, float offsetY) {
        var previousX = OffsetX;
        var previousY = OffsetY;
        OffsetX = IsHorizontalEnabled
            ? Math.Clamp(offsetX, 0, MaxOffsetX)
            : 0;
        OffsetY = IsVerticalEnabled
            ? Math.Clamp(offsetY, 0, MaxOffsetY)
            : 0;
        _velocityX = 0;
        _velocityY = 0;
        _settling = false;
        return OffsetX != previousX || OffsetY != previousY;
    }

    public bool ScrollByWheel(float deltaX, float deltaY) {
        var previousX = OffsetX;
        var previousY = OffsetY;
        if (IsHorizontalEnabled) {
            OffsetX = ApplyOverscroll(
                OffsetX + deltaX,
                MaxOffsetX,
                enabled: true);
        }

        if (IsVerticalEnabled) {
            OffsetY = ApplyOverscroll(
                OffsetY + deltaY,
                MaxOffsetY,
                enabled: true);
        }

        if (OffsetX == previousX && OffsetY == previousY) {
            return false;
        }

        _velocityX = 0;
        _velocityY = 0;
        _settling = true;
        return true;
    }

    public void BeginDrag(float x, float y, long timestamp) {
        _dragging = true;
        _dragStartX = x;
        _dragStartY = y;
        _dragOffsetX = OffsetX;
        _dragOffsetY = OffsetY;
        _lastDragX = x;
        _lastDragY = y;
        _velocityX = 0;
        _velocityY = 0;
        _lastMoveTimestamp = timestamp;
        _settling = false;
    }

    public bool DragTo(float x, float y, long timestamp) {
        var previousX = OffsetX;
        var previousY = OffsetY;
        var nextX = IsHorizontalEnabled
            ? _dragOffsetX - (x - _dragStartX)
            : OffsetX;
        var nextY = IsVerticalEnabled
            ? _dragOffsetY - (y - _dragStartY)
            : OffsetY;
        OffsetX = ApplyOverscroll(nextX, MaxOffsetX, IsHorizontalEnabled);
        OffsetY = ApplyOverscroll(nextY, MaxOffsetY, IsVerticalEnabled);
        var elapsed = Math.Max(1, timestamp - _lastMoveTimestamp);
        _velocityX = IsHorizontalEnabled
            ? Math.Clamp(
                -((x - _lastDragX) * 1000f) / elapsed,
                -MaxVelocity,
                MaxVelocity)
            : 0;
        _velocityY = IsVerticalEnabled
            ? Math.Clamp(
                -((y - _lastDragY) * 1000f) / elapsed,
                -MaxVelocity,
                MaxVelocity)
            : 0;
        _lastDragX = x;
        _lastDragY = y;
        _lastMoveTimestamp = timestamp;
        return OffsetX != previousX || OffsetY != previousY;
    }

    public void EndDrag(bool canceled) {
        if (canceled) {
            _velocityX = 0;
            _velocityY = 0;
        }

        _dragging = false;
        _settling = true;
    }

    public void SetExtents(
        float viewportWidth,
        float viewportHeight,
        float contentWidth,
        float contentHeight) {
        ViewportWidth = Math.Max(0, viewportWidth);
        ViewportHeight = Math.Max(0, viewportHeight);
        ContentWidth = Math.Max(0, contentWidth);
        ContentHeight = Math.Max(0, contentHeight);
        MaxOffsetX = Math.Max(0, contentWidth - viewportWidth);
        MaxOffsetY = Math.Max(0, contentHeight - viewportHeight);
        ResetDisabledAxes();
        if (!_dragging && !_settling) {
            OffsetX = Math.Clamp(OffsetX, 0, MaxOffsetX);
            OffsetY = Math.Clamp(OffsetY, 0, MaxOffsetY);
        }
    }

    public bool Advance(float elapsedSeconds) {
        if (_dragging || !_settling) {
            return false;
        }

        var changed = false;
        var remaining = Math.Clamp(elapsedSeconds, 0, 0.05f);
        var offsetX = OffsetX;
        var offsetY = OffsetY;
        var velocityX = _velocityX;
        var velocityY = _velocityY;
        while (remaining > 0) {
            var step = Math.Min(remaining, AnimationStep);
            var previousX = offsetX;
            var previousY = offsetY;
            MoveAxis(ref offsetX, ref velocityX, MaxOffsetX, step);
            MoveAxis(ref offsetY, ref velocityY, MaxOffsetY, step);
            changed |= Math.Abs(previousX - offsetX) > 0.001f ||
                Math.Abs(previousY - offsetY) > 0.001f;
            remaining -= step;
        }

        OffsetX = offsetX;
        OffsetY = offsetY;
        _velocityX = velocityX;
        _velocityY = velocityY;
        if (Math.Abs(OffsetX - Math.Clamp(OffsetX, 0, MaxOffsetX)) < 0.1f &&
            Math.Abs(OffsetY - Math.Clamp(OffsetY, 0, MaxOffsetY)) < 0.1f &&
            Math.Abs(_velocityX) < 1 &&
            Math.Abs(_velocityY) < 1) {
            OffsetX = Math.Clamp(OffsetX, 0, MaxOffsetX);
            OffsetY = Math.Clamp(OffsetY, 0, MaxOffsetY);
            _velocityX = 0;
            _velocityY = 0;
            _settling = false;
        }

        return changed;
    }

    private void ResetDisabledAxes() {
        if (!IsHorizontalEnabled) {
            OffsetX = 0;
            _velocityX = 0;
        }

        if (!IsVerticalEnabled) {
            OffsetY = 0;
            _velocityY = 0;
        }
    }

    private float ApplyOverscroll(float value, float maximum, bool enabled) {
        if (!enabled) {
            return 0;
        }

        if (value < 0) {
            return Math.Max(-MaxOverscroll, value * OverscrollResistance);
        }

        if (value > maximum) {
            return maximum +
                Math.Min(MaxOverscroll, (value - maximum) * OverscrollResistance);
        }

        return value;
    }

    private void MoveAxis(
        ref float position,
        ref float velocity,
        float maximum,
        float delta) {
        var target = Math.Clamp(position, 0, maximum);
        if (position < 0 || position > maximum) {
            SpringAxis(ref position, ref velocity, target, delta);
            return;
        }

        var next = position + (velocity * delta);
        velocity *= MathF.Exp(-FlingDamping * delta);
        if (next < 0 || next > maximum) {
            position = Math.Clamp(next, -MaxOverscroll, maximum + MaxOverscroll);
            velocity *= 0.65f;
        } else {
            position = next;
        }
    }

    private void SpringAxis(
        ref float position,
        ref float velocity,
        float target,
        float delta) {
        SpringSolver.Step(
            ref position,
            ref velocity,
            target,
            delta,
            ScrollSpring);
        position = Math.Clamp(position, -MaxOverscroll, target + MaxOverscroll);
    }
}
