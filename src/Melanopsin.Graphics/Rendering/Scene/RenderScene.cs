using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class RenderScene {
    private IReadOnlyList<RenderCommand>? _commands;

    public RenderScene(
        PixelSize size,
        Color clearColor,
        IReadOnlyList<RenderCommand> commands,
        DamageRegion? damage = null,
        RenderStatistics? statistics = null,
        AccessibilityVisualOptions visualAccessibility = default,
        RenderLayer? rootLayer = null)
        : this(
            size,
            clearColor,
            rootLayer ?? RenderLayerBuilder.Build(commands),
            damage,
            statistics,
            visualAccessibility) {
        if (RootLayer.CountCommands() != commands.Count) {
            throw new InvalidOperationException(
                "The render layer tree does not preserve the command stream.");
        }

        _commands = commands;
    }

    public RenderScene(
        PixelSize size,
        Color clearColor,
        RenderLayer rootLayer,
        DamageRegion? damage = null,
        RenderStatistics? statistics = null,
        AccessibilityVisualOptions visualAccessibility = default) {
        ArgumentNullException.ThrowIfNull(rootLayer);
        Size = size;
        ClearColor = clearColor;
        RootLayer = rootLayer;
        Damage = damage ?? DamageRegion.Full(size);
        if (!ReferenceEquals(damage, null) && damage.Size != size) {
            throw new ArgumentException(
                "The damage region size does not match the render scene.",
                nameof(damage));
        }

        Statistics = statistics ??
            new RenderStatistics(rootLayer.CountCommands(), 0, 0);
        VisualAccessibility = visualAccessibility;
    }

    public PixelSize Size { get; }

    public Color ClearColor { get; }

    /// <summary>
    /// Compatibility command view. Prefer <see cref="RootLayer"/> in backends.
    /// </summary>
    public IReadOnlyList<RenderCommand> Commands {
        get {
            if (_commands is not null) {
                return _commands;
            }

            var commands = new List<RenderCommand>();
            RootLayer.AppendCommands(commands);
            _commands = commands;
            return commands;
        }
    }

    public RenderLayer RootLayer { get; }

    public DamageRegion Damage { get; }

    public RenderStatistics Statistics { get; }

    public AccessibilityVisualOptions VisualAccessibility { get; }
}
