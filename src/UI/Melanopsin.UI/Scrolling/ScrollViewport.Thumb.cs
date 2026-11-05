// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

public sealed partial class ScrollViewport {
    private float _scrollbarThickness = UiMetrics.Default.Space(1.5f);
    private float _scrollbarMinLength = UiMetrics.Default.Space(8);
    private float _scrollbarInset = UiMetrics.Default.Space(0.75f);

    private bool IsInScrollbarHitArea(Point point) {
        return IsInScrollbarHitArea(point, ScrollAxis.Vertical) ||
            IsInScrollbarHitArea(point, ScrollAxis.Horizontal);
    }

    private bool IsInScrollbarHitArea(Point point, ScrollAxis axis) {
        if (!TryGetTrackHitArea(axis, out var hitArea)) {
            return false;
        }

        return hitArea.Contains(point);
    }

    private bool TryGetTrackHitArea(ScrollAxis axis, out Rect hitArea) {
        if (!TryGetTrack(axis, out var track)) {
            hitArea = default;
            return false;
        }

        var hitThickness = Math.Max(
            _scrollbarThickness * 2,
            _scrollbarThickness + 8);
        hitArea = axis == ScrollAxis.Vertical
            ? new Rect(
                track.MaxX - hitThickness,
                track.MinY,
                hitThickness,
                track.Height)
            : new Rect(
                track.MinX,
                track.MaxY - hitThickness,
                track.Width,
                hitThickness);
        return true;
    }

    private bool TryGetTrack(ScrollAxis axis, out Rect track) {
        if (_contentBounds.IsEmpty) {
            track = default;
            return false;
        }

        var start = _scrollbarInset;
        var end = (axis == ScrollAxis.Vertical
            ? _contentBounds.Height
            : _contentBounds.Width) - _scrollbarInset;
        if (end <= start) {
            track = default;
            return false;
        }

        track = axis == ScrollAxis.Vertical
            ? new Rect(
                _contentBounds.MaxX - _scrollbarInset - _scrollbarThickness,
                _contentBounds.MinY + start,
                _scrollbarThickness,
                end - start)
            : new Rect(
                _contentBounds.MinX + start,
                _contentBounds.MaxY - _scrollbarInset - _scrollbarThickness,
                end - start,
                _scrollbarThickness);
        return true;
    }

    private bool TryGetThumbBar(
        ScrollAxis axis,
        out ThumbBar bar,
        out float maximumOffset) {
        var enabled = axis == ScrollAxis.Vertical
            ? _scroll.IsVerticalEnabled
            : _scroll.IsHorizontalEnabled;
        var viewportExtent = axis == ScrollAxis.Vertical
            ? _scroll.ViewportHeight
            : _scroll.ViewportWidth;
        var contentExtent = axis == ScrollAxis.Vertical
            ? _scroll.ContentHeight
            : _scroll.ContentWidth;
        maximumOffset = axis == ScrollAxis.Vertical
            ? _scroll.MaxOffsetY
            : _scroll.MaxOffsetX;
        if (!enabled ||
            maximumOffset <= 0.01f ||
            viewportExtent <= 0 ||
            contentExtent <= viewportExtent ||
            !TryGetTrack(axis, out var track)) {
            bar = default;
            return false;
        }

        var trackLength = axis == ScrollAxis.Vertical
            ? track.Height
            : track.Width;
        var thumbLength = Math.Min(
            trackLength,
            Math.Max(
                Math.Min(_scrollbarMinLength, trackLength),
                trackLength * (viewportExtent / contentExtent)));
        var progress = Math.Clamp(
            (axis == ScrollAxis.Vertical ? OffsetY : OffsetX) / maximumOffset,
            0,
            1);
        var thumbStart = trackLength - thumbLength > 0
            ? progress * (trackLength - thumbLength)
            : 0;
        var thumb = axis == ScrollAxis.Vertical
            ? new Rect(
                track.MinX,
                track.MinY + thumbStart,
                track.Width,
                thumbLength)
            : new Rect(
                track.MinX + thumbStart,
                track.MinY,
                thumbLength,
                track.Height);
        bar = new ThumbBar(thumb, trackLength, thumbLength);
        return true;
    }

    private readonly record struct ThumbBar(
        Rect Thumb,
        float TrackLength,
        float ThumbLength);
}
