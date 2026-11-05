// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

internal sealed class RenderLayerClipFrame(
    RenderLayerFrame? parent,
    Rect bounds)
    : RenderLayerFrame(parent) {
    public override RenderLayer Build() {
        FlushCommands();
        return new ClipLayer(bounds, new ContainerLayer(Layers));
    }
}
