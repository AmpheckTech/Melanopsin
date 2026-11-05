// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class PushClipCommand : RenderCommand {
    public PushClipCommand(Rect bounds) {
        Bounds = bounds;
    }

    public Rect Bounds { get; }
}
