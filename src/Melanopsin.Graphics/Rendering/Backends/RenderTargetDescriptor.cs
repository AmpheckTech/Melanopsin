using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public readonly record struct RenderTargetDescriptor {
    public RenderTargetDescriptor(PixelSize size) {
        Size = size;
    }

    public PixelSize Size { get; }
}
