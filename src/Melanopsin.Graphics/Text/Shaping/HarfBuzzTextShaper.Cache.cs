// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

public sealed partial class HarfBuzzTextShaper {
    private readonly Dictionary<TextLayoutKey, ShapedText> _layouts = [];
    private readonly Lock _layoutLock = new();
    private bool _disposed;

    public ShapedText Layout(string text, in TextLayoutOptions options) {
        ArgumentNullException.ThrowIfNull(text);
        _lifetimeLock.EnterReadLock();
        try {
            var key = new TextLayoutKey(
                text,
                options.FontSize,
                options.MaxWidth,
                options.Direction,
                options.Font,
                _fonts.Version);
            lock (_layoutLock) {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_layouts.TryGetValue(key, out var cached)) {
                    return cached;
                }
            }

            var layout = ShapeText(text, options);
            lock (_layoutLock) {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_layouts.TryGetValue(key, out var cached)) {
                    return cached;
                }

                if (_layouts.Count >= 1024) {
                    _layouts.Clear();
                }

                _layouts[key] = layout;
                return layout;
            }
        } finally {
            _lifetimeLock.ExitReadLock();
        }
    }
}
