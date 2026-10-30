namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// An ordered group of child layers.
/// </summary>
public sealed class ContainerLayer : RenderLayer {
    private readonly RenderLayer[] _children;

    public ContainerLayer(IReadOnlyList<RenderLayer> children) {
        ArgumentNullException.ThrowIfNull(children);
        _children = [.. children];
    }

    public IReadOnlyList<RenderLayer> Children => _children;

    internal override void AppendCommands(List<RenderCommand> commands) {
        foreach (var child in _children) {
            child.AppendCommands(commands);
        }
    }

    public override int CountCommands() {
        var count = 0;
        foreach (var child in _children) {
            count += child.CountCommands();
        }

        return count;
    }
}
