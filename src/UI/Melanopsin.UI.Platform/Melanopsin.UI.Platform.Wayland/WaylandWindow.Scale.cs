// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using NWayland.Protocols.FractionalScaleV1;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed partial class WaylandWindow {
    private WpFractionalScaleV1? _fractionalScale;

    private void OnPreferredBufferScale(int factor) {
        if (_disposed || HasFractionalScaling || factor <= 0) {
            return;
        }

        SetDisplayScale(factor);
    }

    private void OnPreferredFractionalScale(uint scale) {
        if (_disposed || scale == 0) {
            return;
        }

        SetDisplayScale(scale / 120f);
    }

    private void SetDisplayScale(float scale) {
        scale = NormalizeScale(scale);
        if (Math.Abs(DisplayScale - scale) < 0.0001f) {
            return;
        }

        _ = UpdateConfiguration(
            LogicalSize,
            scale,
            out var scaleChanged,
            out var sizeChanged);
        if (scaleChanged || sizeChanged) {
            _onConfigured(
                this,
                scaleChanged,
                sizeChanged,
                scaleChanged || sizeChanged);
        }
    }

    private sealed class SurfaceListener : WlSurface.Listener {
        private readonly WaylandWindow _window;

        public SurfaceListener(WaylandWindow window) {
            _window = window;
        }

        protected override void PreferredBufferScale(
            WlSurface eventSender,
            int factor) {
            _window.OnPreferredBufferScale(factor);
        }
    }

    private sealed class FractionalScaleListener : WpFractionalScaleV1.Listener {
        private readonly WaylandWindow _window;

        public FractionalScaleListener(WaylandWindow window) {
            _window = window;
        }

        protected override void PreferredScale(
            WpFractionalScaleV1 eventSender,
            uint scale) {
            _window.OnPreferredFractionalScale(scale);
        }
    }
}
