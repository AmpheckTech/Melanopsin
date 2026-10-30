namespace Melanopsin.Graphics.Rendering;

internal sealed class RenderLayerTransformFrame(
    RenderLayerFrame? parent,
    float x,
    float y)
    : RenderLayerFrame(parent) {
    public override RenderLayer Build() {
        FlushCommands();
        return new TransformLayer(x, y, new ContainerLayer(Layers));
    }
}
