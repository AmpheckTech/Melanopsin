namespace Melanopsin.Graphics.Rendering;

internal abstract class RenderLayerFrame {
    private readonly List<RenderCommand> _commands = [];
    private readonly List<RenderLayer> _layers = [];

    protected RenderLayerFrame(RenderLayerFrame? parent) {
        Parent = parent;
    }

    public RenderLayerFrame? Parent { get; }

    protected List<RenderLayer> Layers => _layers;

    public void AddCommand(RenderCommand command) {
        _commands.Add(command);
    }

    public void AddLayer(RenderLayer layer) {
        FlushCommands();
        _layers.Add(layer);
    }

    public abstract RenderLayer Build();

    protected void FlushCommands() {
        if (_commands.Count == 0) {
            return;
        }

        _layers.Add(new ContentLayer(_commands));
        _commands.Clear();
    }
}
