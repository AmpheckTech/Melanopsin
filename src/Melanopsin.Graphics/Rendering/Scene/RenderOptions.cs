using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public readonly record struct RenderOptions {
    public Color ClearColor { get; init; }

    public float ScaleFactor { get; init; }

    public bool DrawDebugBounds { get; init; }

    public AccessibilityVisualOptions VisualAccessibility { get; init; }

    public static RenderOptions Default { get; } = new() {
        ClearColor = new Color(12, 18, 24),
        ScaleFactor = 1f,
    };
}
