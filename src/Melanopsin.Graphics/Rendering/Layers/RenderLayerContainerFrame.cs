namespace Melanopsin.Graphics.Rendering;

internal sealed class RenderLayerContainerFrame(RenderLayerFrame? parent)
    : RenderLayerFrame(parent) {
    public override RenderLayer Build() {
        FlushCommands();
        return new ContainerLayer(Layers);
    }
}
