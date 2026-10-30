namespace Melanopsin.Graphics.Rendering;

public interface IRenderTarget : IDisposable {
    RenderTargetDescriptor Descriptor { get; }

    RenderFrame Render(RenderScene scene);
}
