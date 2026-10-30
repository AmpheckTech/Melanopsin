using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class FillRectangleCommand : RenderCommand {
    public FillRectangleCommand(Rect bounds, Color color, float cornerRadius = 0) {
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        Bounds = bounds;
        Color = color;
        CornerRadius = cornerRadius;
    }

    public Rect Bounds { get; }

    public Color Color { get; }

    public float CornerRadius { get; }
}
