using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI;

public sealed partial class ScrollViewport {
    private static readonly Color Transparent = new(0, 0, 0, 0);

    private readonly HashSet<string> _trackStates = new(StringComparer.Ordinal);
    private readonly HashSet<string> _verticalThumbStates = new(StringComparer.Ordinal);
    private readonly HashSet<string> _horizontalThumbStates = new(StringComparer.Ordinal);
    private ScrollbarStyleAnimation? _trackStyle;
    private ScrollbarStyleAnimation? _verticalThumbStyle;
    private ScrollbarStyleAnimation? _horizontalThumbStyle;
    private Point _pointerPosition;
    private bool _hasPointerPosition;

    private bool AdvanceScrollbarStyleAnimations(float elapsedSeconds) {
        var changed = _trackStyle?.Advance(elapsedSeconds) == true;
        changed |= _verticalThumbStyle?.Advance(elapsedSeconds) == true;
        changed |= _horizontalThumbStyle?.Advance(elapsedSeconds) == true;
        return changed;
    }

    private void RefreshScrollbarStates() {
        _trackStates.Clear();
        if (_pointerInScrollbar) {
            _trackStates.Add(UiStateNames.Hover);
        }

        RefreshThumbStates(
            ScrollAxis.Vertical,
            _verticalThumbStates);
        RefreshThumbStates(
            ScrollAxis.Horizontal,
            _horizontalThumbStates);
    }

    private void RefreshThumbStates(
        ScrollAxis axis,
        HashSet<string> states) {
        states.Clear();
        if (IsPointerOverThumb(axis)) {
            states.Add(UiStateNames.Hover);
        }

        if (_thumbDragging && _thumbAxis == axis) {
            states.Add(UiStateNames.Pressed);
        }
    }

    private void RefreshScrollbarStyles(ResolvedStyle ownerStyle, StyleEngine styles) {
        RefreshScrollbarStates();
        UpdateStyle(
            ref _trackStyle,
            "track",
            CreateScrollbarPartStyle(
                ownerStyle,
                ownerStyle.ScrollbarTrackColor ?? Transparent),
            _trackStates,
            styles);
        UpdateStyle(
            ref _verticalThumbStyle,
            "thumb",
            CreateScrollbarPartStyle(
                ownerStyle,
                ownerStyle.ScrollbarThumbColor ?? DefaultThumbColor),
            _verticalThumbStates,
            styles);
        UpdateStyle(
            ref _horizontalThumbStyle,
            "thumb",
            CreateScrollbarPartStyle(
                ownerStyle,
                ownerStyle.ScrollbarThumbColor ?? DefaultThumbColor),
            _horizontalThumbStates,
            styles);
    }

    private void UpdateStyle(
        ref ScrollbarStyleAnimation? animation,
        string part,
        ResolvedStyle baseStyle,
        IReadOnlySet<string> states,
        StyleEngine styles) {
        var target = styles.ResolvePseudoElement(this, part, baseStyle, states);
        if (animation is null) {
            animation = new ScrollbarStyleAnimation(target);
        } else {
            animation.SetTarget(target);
        }
    }

    private static ResolvedStyle CreateScrollbarPartStyle(
        ResolvedStyle ownerStyle,
        Color background) {
        return new ResolvedStyle {
            Background = background,
            BorderColor = Transparent,
            BorderWidth = 0,
            BorderRadius = ownerStyle.ScrollbarThickness / 2,
            Opacity = 1,
            TransitionDuration = 0,
            TransitionEasing = ownerStyle.TransitionEasing,
        };
    }

    private ResolvedStyle GetTrackStyle(ResolvedStyle ownerStyle) {
        return _trackStyle?.Current ??
            CreateScrollbarPartStyle(
                ownerStyle,
                ownerStyle.ScrollbarTrackColor ?? Transparent);
    }

    private ResolvedStyle GetThumbStyle(
        ScrollAxis axis,
        ResolvedStyle ownerStyle) {
        var animation = axis == ScrollAxis.Vertical
            ? _verticalThumbStyle
            : _horizontalThumbStyle;
        return animation?.Current ??
            CreateScrollbarPartStyle(
                ownerStyle,
                ownerStyle.ScrollbarThumbColor ?? DefaultThumbColor);
    }

    private bool IsPointerOverThumb(ScrollAxis axis) {
        return _hasPointerPosition &&
            TryGetThumbBar(axis, out var bar, out _) &&
            bar.Thumb.Contains(_pointerPosition) &&
            IsInScrollbarHitArea(_pointerPosition, axis);
    }

    private static void AppendScrollbarRectangle(
        List<RenderCommand> commands,
        Rect bounds,
        ResolvedStyle pseudoElementStyle,
        ResolvedStyle ownerStyle,
        float visibility) {
        var opacity = ownerStyle.Opacity * pseudoElementStyle.Opacity;
        if (pseudoElementStyle.Background.Alpha != 0) {
            commands.Add(new FillRectangleCommand(
                bounds,
                ApplyVisibility(pseudoElementStyle.Background, opacity, visibility),
                pseudoElementStyle.BorderRadius));
        }

        if (pseudoElementStyle.BorderWidth > 0 &&
            pseudoElementStyle.BorderColor.Alpha != 0) {
            commands.Add(new StrokeRectangleCommand(
                bounds,
                ApplyVisibility(
                    pseudoElementStyle.BorderColor,
                    opacity,
                    visibility),
                pseudoElementStyle.BorderWidth,
                pseudoElementStyle.BorderRadius));
        }
    }

    private sealed class ScrollbarStyleAnimation {
        private ResolvedStyle _target;
        private StyleTransitionState? _transition;

        public ScrollbarStyleAnimation(ResolvedStyle style) {
            _target = ResolvedStyleInterpolator.Clone(style);
        }

        public ResolvedStyle Current => _transition?.Current ?? _target;

        public bool SetTarget(ResolvedStyle target) {
            if (ResolvedStyleComparer.AreEquivalent(_target, target)) {
                return false;
            }

            var changed = !ResolvedStyleComparer.AreEquivalent(Current, target);
            target = ResolvedStyleInterpolator.Clone(target);
            _transition = target.TransitionDuration > 0 && changed
                ? new StyleTransitionState(Current, target)
                : null;
            _target = target;
            return true;
        }

        public bool Advance(float elapsedSeconds) {
            if (_transition is null) {
                return false;
            }

            _ = _transition.Advance(elapsedSeconds);
            if (_transition.IsComplete) {
                _transition = null;
                return true;
            }

            _transition.SetCurrent(ResolvedStyleInterpolator.Interpolate(
                _transition.Source,
                _transition.Target,
                _transition.EasedProgress));
            return true;
        }
    }
}
