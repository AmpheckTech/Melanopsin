// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public readonly record struct RenderTargetDescriptor {
    public RenderTargetDescriptor(PixelSize size) {
        Size = size;
    }

    public PixelSize Size { get; }
}
