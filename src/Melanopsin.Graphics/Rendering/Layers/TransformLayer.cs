namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Applies a translation before rendering its child.
/// </summary>
public sealed class TransformLayer : RenderLayer {
    public TransformLayer(float x, float y, RenderLayer child) {
        ArgumentNullException.ThrowIfNull(child);
        X = x;
        Y = y;
        Child = child;
    }

    public float X { get; }

    public float Y { get; }

    public RenderLayer Child { get; }

    internal override void AppendCommands(List<RenderCommand> commands) {
        commands.Add(new PushTranslateCommand(X, Y));
        Child.AppendCommands(commands);
        commands.Add(new PopTranslateCommand());
    }

    public override int CountCommands() {
        return Child.CountCommands() + 2;
    }
}
