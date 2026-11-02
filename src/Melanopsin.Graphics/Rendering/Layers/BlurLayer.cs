namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Composites its child into an isolated layer and applies a Gaussian blur to
/// the complete layer before drawing it back to the parent.
/// </summary>
public sealed class BlurLayer : RenderLayer {
    public BlurLayer(float radius, Rect bounds, RenderLayer child) {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        if (!float.IsFinite(radius)) {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        ArgumentNullException.ThrowIfNull(child);
        Radius = radius;
        Bounds = bounds;
        Child = child;
    }

    /// <summary>The Gaussian standard deviation in layout pixels.</summary>
    public float Radius { get; }

    /// <summary>The untransformed filter region.</summary>
    public Rect Bounds { get; }

    public RenderLayer Child { get; }

    internal override void AppendCommands(List<RenderCommand> commands) {
        Child.AppendCommands(commands);
    }

    public override int CountCommands() {
        return Child.CountCommands();
    }
}
