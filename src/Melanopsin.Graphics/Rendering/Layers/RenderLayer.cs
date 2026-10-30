namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Backend-neutral retained rendering layer.
/// </summary>
public abstract class RenderLayer {
    private protected RenderLayer() {
    }

    internal abstract void AppendCommands(List<RenderCommand> commands);

    public abstract int CountCommands();
}
