namespace Melanopsin.UI.Rendering;

/// <summary>
/// Lets an external control contribute backend-neutral drawing commands
/// without adding a dependency from UI Core to that control library.
/// </summary>
public interface IRenderCommandProvider {
    void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands);
}
