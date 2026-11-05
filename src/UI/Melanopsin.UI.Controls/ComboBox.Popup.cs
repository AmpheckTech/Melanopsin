// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Controls;

public sealed partial class ComboBox {
    private readonly Dictionary<Node, (float X, float Y)> _scrollObservers = [];
    private ComboBoxPopup? _popup;
    private OverlayHost? _overlayHost;
    private Rect _triggerLayoutBounds;
    private Rect? _triggerClipBounds;

    protected internal override void OnMounted() {
        base.OnMounted();
        SyncPopup();
    }

    protected internal override void OnUnmounted() {
        ClosePopup();
        ClearScrollObservers();
        _overlayHost = null;
        _triggerClipBounds = null;
        base.OnUnmounted();
    }

    private void UpdateTriggerBounds(Rect bounds, Rect? clipBounds) {
        _triggerLayoutBounds = bounds;
        _triggerClipBounds = clipBounds;
        TriggerHeight = Math.Max(1, bounds.Height);
        RefreshScrollObservers();
    }

    private void SyncPopup() {
        var host = FindOverlayHost();
        if (!ReferenceEquals(host, _overlayHost)) {
            ClosePopup();
            _overlayHost = host;
        }

        if (host is null ||
            !IsMounted ||
            !IsOpen ||
            _options.Count == 0 ||
            TriggerHeight <= 0 ||
            _triggerLayoutBounds.Width <= 0) {
            ClosePopup();
            return;
        }

        if (_popup is null ||
            !host.Overlays.Contains(_popup, ReferenceEqualityComparer.Instance)) {
            ClosePopup();
            _popup = new ComboBoxPopup(this);
            _popup.Closed += OnPopupClosed;
            _ = host.Show(_popup);
        }

        var anchorBounds = GetVisibleTriggerBounds();
        if (anchorBounds is null) {
            ClosePopup();
            IsOpen = false;
            return;
        }

        _popup.UpdateAnchor(anchorBounds.Value);
        _popup.EnsureRowVisible(SelectedIndex);
    }

    private void ClosePopup() {
        var popup = _popup;
        if (popup is null) {
            return;
        }

        _popup = null;
        popup.Closed -= OnPopupClosed;
        if (_overlayHost is { } host &&
            host.Overlays.Contains(popup, ReferenceEqualityComparer.Instance)) {
            _ = host.Close(popup);
        } else if (popup.IsOpen) {
            popup.Close();
        }
    }

    private void OnPopupClosed(Overlay popup) {
        if (!ReferenceEquals(popup, _popup)) {
            return;
        }

        popup.Closed -= OnPopupClosed;
        _popup = null;
        if (IsOpen) {
            SetExpanded(false);
        }
    }

    private OverlayHost? FindOverlayHost() {
        for (var current = Parent; current is not null; current = current.Parent) {
            if (current is OverlayHost host) {
                return host;
            }
        }

        return null;
    }

    private Rect? GetVisibleTriggerBounds() {
        var x = _triggerLayoutBounds.MinX;
        var y = _triggerLayoutBounds.MinY;
        for (var current = Parent; current is not null; current = current.Parent) {
            if (current is IScrollTransform scrollTransform) {
                x -= scrollTransform.OffsetX;
                y -= scrollTransform.OffsetY;
            }
        }

        var bounds = new Rect(
            x,
            y,
            _triggerLayoutBounds.Width,
            _triggerLayoutBounds.Height);
        return _triggerClipBounds is { } clipBounds &&
            !bounds.IntersectsWith(clipBounds)
                ? null
                : bounds;
    }

    private void RefreshScrollObservers() {
        ClearScrollObservers();
        for (var current = Parent; current is not null; current = current.Parent) {
            if (current is IScrollTransform) {
                current.Invalidated += OnScrollInvalidated;
                _scrollObservers.Add(
                    current,
                    (((IScrollTransform)current).OffsetX,
                     ((IScrollTransform)current).OffsetY));
            }
        }
    }

    private void ClearScrollObservers() {
        foreach (var observer in _scrollObservers.Keys) {
            observer.Invalidated -= OnScrollInvalidated;
        }

        _scrollObservers.Clear();
    }

    private void OnScrollInvalidated(NodeInvalidation invalidation) {
        if (_popup is null ||
            !_scrollObservers.TryGetValue(
                invalidation.Source,
                out var previousOffset) ||
            invalidation.Source is not IScrollTransform scrollTransform) {
            return;
        }

        var offset = (scrollTransform.OffsetX, scrollTransform.OffsetY);
        if (Math.Abs(offset.OffsetX - previousOffset.X) < 0.001f &&
            Math.Abs(offset.OffsetY - previousOffset.Y) < 0.001f) {
            return;
        }

        _scrollObservers[invalidation.Source] = offset;
        SetExpanded(false);
    }

    private void OnComboBoxInvalidated(NodeInvalidation invalidation) {
        if (_popup is null) {
            return;
        }

        if ((invalidation.Kind & InvalidationKind.Layout) != 0) {
            _popup.InvalidateLayout();
        } else if ((invalidation.Kind & InvalidationKind.Paint) != 0) {
            _popup.InvalidatePaint();
        }
    }
}
