using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

[MarkupElement]
public sealed class OverlayHost :
    Control,
    ILayoutParticipant,
    ILayoutViewportDependency {
    private readonly OverlayBackdrop _backdrop;
    private readonly List<Overlay> _overlays = [];
    private Node? _content;

    public OverlayHost()
        : base("OverlayHost") {
        ClipToBounds = true;
        IsRepaintBoundary = true;
        AllowsRasterCache = false;
        IsRelayoutBoundary = true;
        Accessibility.Role = AccessibilityRole.Region;
        InlineStyle.Width = Length.Percent(100);
        InlineStyle.Height = Length.Percent(100);
        _backdrop = new OverlayBackdrop {
            Clicked = DismissTopOverlayFromBackdrop,
        };
        base.AppendChild(_backdrop);
    }

    bool ILayoutViewportDependency.UsesViewportUnits => false;

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
        var contentConstraint = LayoutConstraints.Loose(
            Math.Max(
                0,
                width - (style.Padding * 2) - (style.Margin * 2)),
            constraints.HasBoundedHeight
                ? Math.Max(0, constraints.MaxHeight - (style.Padding * 2))
                : float.PositiveInfinity);
        var childrenWidth = 0f;
        var childrenHeight = 0f;
        foreach (var child in context.GetVisibleChildren(this)) {
            var measured = context.Measure(child, contentConstraint);
            childrenWidth = Math.Max(childrenWidth, measured.Width);
            childrenHeight = Math.Max(childrenHeight, measured.Height);
        }

        _ = childrenWidth;
        var fallbackHeight = Math.Max(
            context.Metrics.Space(8),
            childrenHeight);
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
        var content = context.ContentBounds;
        foreach (var child in context.GetVisibleChildren(this)) {
            if (child is Overlay overlay) {
                var measured = context.Measure(
                    overlay,
                    LayoutConstraints.Loose(
                        content.Width,
                        content.Height));
                context.ArrangeChild(
                    overlay,
                    overlay.GetArrangeBounds(context, measured));
            } else {
                context.ArrangeChild(child, content);
            }
        }
    }

    public Node? Content {
        get => _content;
        set {
            if (ReferenceEquals(_content, value)) {
                return;
            }

            if (_content is not null) {
                base.RemoveChild(_content);
            }

            _content = value;
            if (value is not null) {
                base.InsertChild(0, value);
            }
        }
    }

    public IReadOnlyList<Overlay> Overlays => _overlays;

    public T Show<T>(T overlay)
        where T : Overlay {
        ArgumentNullException.ThrowIfNull(overlay);
        if (overlay.Parent is not null) {
            throw new InvalidOperationException("The overlay already belongs to a parent.");
        }

        overlay.Reopen();
        overlay.Closed += OnOverlayClosed;
        _overlays.Add(overlay);
        base.AppendChild(overlay);
        UpdateBackdrop();
        return overlay;
    }

    public bool Close(Overlay overlay, string? result = null) {
        ArgumentNullException.ThrowIfNull(overlay);
        if (!_overlays.Contains(overlay, ReferenceEqualityComparer.Instance)) {
            return false;
        }

        overlay.Close(result);
        return true;
    }

    public void ClearOverlays() {
        foreach (var overlay in _overlays.ToArray()) {
            _ = Close(overlay);
        }
    }

    public Overlay? TopOverlay => _overlays.Count == 0 ? null : _overlays[^1];

    public bool CloseTop(string? result = null) {
        var overlay = TopOverlay;
        return overlay is not null && Close(overlay, result);
    }

    public bool HasModalOverlay =>
        _overlays.Any(static overlay => overlay.IsModal && overlay.IsOpen);

    private void OnOverlayClosed(Overlay overlay) {
        overlay.Closed -= OnOverlayClosed;
        _ = _overlays.Remove(overlay);
        if (ReferenceEquals(overlay.Parent, this)) {
            base.RemoveChild(overlay);
        }

        UpdateBackdrop();
    }

    private void DismissTopOverlayFromBackdrop() {
        if (TopOverlay is { DismissOnBackdrop: true } overlay) {
            _ = Close(overlay);
        }
    }

    private void UpdateBackdrop() {
        _backdrop.SetVisible(_overlays.Any(static overlay => overlay.IsModal));
    }
}
