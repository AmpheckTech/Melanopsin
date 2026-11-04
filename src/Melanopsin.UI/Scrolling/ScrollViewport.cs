using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

[MarkupElement]
public sealed partial class ScrollViewport :
    Control,
    IInputHandler,
    ILayoutParticipant,
    IAdditionalHitRegionProvider,
    ILayoutViewportDependency,
    IOverlayRenderCommandProvider,
    IScrollTransform {
    private const float DragThreshold = 8;

    private readonly ScrollController _scroll = new();
    private PointerId _dragPointer;
    private float _candidateX;
    private float _candidateY;
    private bool _hasDragCandidate;
    private Rect _contentBounds;

    public ScrollViewport()
        : base("ScrollViewport") {
        ClipToBounds = true;
        IsRepaintBoundary = true;
        AllowsRasterCache = false;
        IsRelayoutBoundary = true;
        InputHandler = this;
        Accessibility.Role = AccessibilityRole.ScrollView;
    }

    bool ILayoutViewportDependency.UsesViewportUnits => false;

    public float OffsetX => _scroll.OffsetX;

    public float OffsetY => _scroll.OffsetY;

    public float MaxOffsetX => _scroll.MaxOffsetX;

    public float MaxOffsetY => _scroll.MaxOffsetY;

    public float ScrollStep { get; set; } = 32;

    [MarkupAttribute("axis", MarkupValueKind.Enum)]
    public ScrollAxis Axis {
        get => _scroll.Axis;
        private set => _scroll.SetAxis(value);
    }

    public float MaxOverscroll {
        get => _scroll.MaxOverscroll;
        set => _scroll.MaxOverscroll = Math.Max(0, value);
    }

    public void ScrollBy(float deltaX, float deltaY) {
        if (_scroll.ScrollBy(deltaX, deltaY)) {
            InvalidatePaint();
        }
    }

    internal bool AdvanceAnimation(float elapsedSeconds) {
        var changed = _scroll.Advance(elapsedSeconds);
        changed |= AdvanceThumbAnimation(elapsedSeconds);
        changed |= AdvanceScrollbarStyleAnimations(elapsedSeconds);
        if (!changed) {
            return false;
        }

        InvalidatePaint();
        return true;
    }

    public void Handle(RoutedInputEvent inputEvent) {
        UpdateThumbPointerState(inputEvent);
        if (HandleThumbInput(inputEvent)) {
            return;
        }

        var input = inputEvent.Input;
        if (input.Phase == PointerPhase.Scroll) {
            var horizontalDelta = _scroll.IsHorizontalEnabled
                ? input.HorizontalWheelDelta
                : 0;
            var verticalDelta = _scroll.IsVerticalEnabled
                ? input.WheelDelta
                : 0;
            if (horizontalDelta == 0 && verticalDelta == 0) {
                return;
            }

            if (_scroll.ScrollByWheel(
                    -(horizontalDelta / 120f * ScrollStep),
                    -(verticalDelta / 120f * ScrollStep))) {
                InvalidatePaint();
            }

            inputEvent.Handled = true;
            return;
        }

        if (input.Phase == PointerPhase.Down) {
            if (!input.IsPrimaryButton) {
                return;
            }

            _dragPointer = input.PointerId;
            _candidateX = input.X;
            _candidateY = input.Y;
            _hasDragCandidate = !_scroll.IsDragging;
            inputEvent.Handled = true;
            return;
        }

        if (input.PointerId != _dragPointer) {
            return;
        }

        if (!input.IsPrimaryButton) {
            return;
        }

        if (input.Phase == PointerPhase.Move) {
            if (!_scroll.IsDragging) {
                if (!_hasDragCandidate || !ExceedsDragThreshold(input.X, input.Y)) {
                    inputEvent.Handled = true;
                    return;
                }

                _hasDragCandidate = false;
                inputEvent.CancelPendingInteraction();
                _scroll.BeginDrag(input.X, input.Y, input.Timestamp);
                inputEvent.Handled = true;
                return;
            }

            if (_scroll.DragTo(input.X, input.Y, input.Timestamp)) {
                InvalidatePaint();
            }

            inputEvent.Handled = true;
        } else if (input.Phase is PointerPhase.Up or PointerPhase.Cancel) {
            _hasDragCandidate = false;
            if (_scroll.IsDragging) {
                _scroll.EndDrag(canceled: input.Phase == PointerPhase.Cancel);
            }

            inputEvent.Handled = true;
        }
    }

    private bool ExceedsDragThreshold(float x, float y) {
        var horizontalDelta = Math.Abs(x - _candidateX);
        var verticalDelta = Math.Abs(y - _candidateY);
        return (_scroll.IsHorizontalEnabled && horizontalDelta >= DragThreshold) ||
            (_scroll.IsVerticalEnabled && verticalDelta >= DragThreshold);
    }

    internal void SetExtents(
        float viewportWidth,
        float viewportHeight,
        float contentWidth,
        float contentHeight) {
        _scroll.SetExtents(
            viewportWidth,
            viewportHeight,
            contentWidth,
            contentHeight);
    }

    LayoutSize ILayoutParticipant.Measure(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        var style = context.GetStyle(this);
        var widthFallback = constraints.HasBoundedWidth
            ? constraints.MaxWidth
            : 0;
        var contentWidth = context.ResolveWidth(
            style,
            constraints,
            widthFallback);
        var width = constraints.ConstrainWidth(
            contentWidth + (style.Margin * 2));
        var fallbackHeight = Math.Min(
            context.Metrics.Space(50),
            constraints.MaxHeight);
        var height = context.ResolveHeight(
            style,
            constraints,
            fallbackHeight);
        height = constraints.ConstrainHeight(height + (style.Margin * 2));
        return new LayoutSize(width, height);
    }

    void ILayoutParticipant.Arrange(
        LayoutArrangeContext context,
        Rect bounds) {
        UpdateScrollbarMetrics(context.GetStyle(this));
        var content = context.ContentBounds;
        _contentBounds = content;
        var children = context.GetVisibleChildren(this);
        if (children.Count == 0) {
            SetExtents(content.Width, content.Height, 0, 0);
            return;
        }

        var child = children[0];
        var measured = context.Measure(
            child,
            LayoutConstraints.Loose(
                content.Width,
                float.PositiveInfinity,
                widthReference: content.Width,
                heightReference: content.Height));
        SetExtents(
            content.Width,
            content.Height,
            measured.Width,
            measured.Height);
        context.ArrangeChild(
            child,
            new Rect(
                content.X,
                content.Y,
                content.Width,
                measured.Height));
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "axis") {
            Axis = value?.ToString()?.ToLowerInvariant() switch {
                "horizontal" => ScrollAxis.Horizontal,
                "both" => ScrollAxis.Both,
                _ => ScrollAxis.Vertical,
            };
        }
    }
}
