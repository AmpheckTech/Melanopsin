// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Hosting;

public sealed class WindowLifecycleEventArgs : EventArgs {
    public WindowLifecycleEventArgs(
        WindowLifecycleKind kind,
        WindowId windowId,
        PixelSize size,
        int x = 0,
        int y = 0) {
        Kind = kind;
        WindowId = windowId;
        Size = size;
        X = x;
        Y = y;
    }

    public WindowLifecycleKind Kind { get; }

    public WindowId WindowId { get; }

    public PixelSize Size { get; }

    public int X { get; }

    public int Y { get; }
}
