namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// An immutable sequence of drawing commands.
/// </summary>
public sealed class ContentLayer : RenderLayer {
    private readonly RenderCommand[] _commands;

    public ContentLayer(IReadOnlyList<RenderCommand> commands) {
        ArgumentNullException.ThrowIfNull(commands);
        _commands = [.. commands];
    }

    public IReadOnlyList<RenderCommand> Commands => _commands;

    internal override void AppendCommands(List<RenderCommand> commands) {
        commands.AddRange(_commands);
    }

    public override int CountCommands() {
        return _commands.Length;
    }
}
