namespace Melanopsin.Graphics.Rendering;

public interface IRenderTarget : IDisposable {
    RenderTargetDescriptor Descriptor { get; }

    bool IsFrameAvailable { get; }

    bool TryRender(RenderScene scene, out RenderFrameLease frame);
}
