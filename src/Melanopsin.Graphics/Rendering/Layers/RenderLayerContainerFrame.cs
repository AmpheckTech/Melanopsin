// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

internal sealed class RenderLayerContainerFrame(RenderLayerFrame? parent)
    : RenderLayerFrame(parent) {
    public override RenderLayer Build() {
        FlushCommands();
        return new ContainerLayer(Layers);
    }
}
