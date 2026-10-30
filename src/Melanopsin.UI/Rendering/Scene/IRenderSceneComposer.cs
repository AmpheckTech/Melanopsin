namespace Melanopsin.UI.Rendering;

public interface IRenderSceneComposer {
    RenderScene Compose(
        LayoutSnapshot layout,
        RenderCompositionContext context);
}
